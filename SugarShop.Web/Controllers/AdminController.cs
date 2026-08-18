using MD.PersianDateTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Infrastructure.Services;
using SugarShop.Web.Extensions;
using SugarShop.Web.Helpers;
using System.Text;
using System.Text.Json;
namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner,OrderManager")]
    public class AdminController : Controller
    {
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly InventoryService _inventoryService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        public AdminController(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            InventoryService inventoryService,
            IWebHostEnvironment webHostEnvironment,
            UserManager<ApplicationUser> userManager)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
            _inventoryService = inventoryService;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.IsAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            ViewBag.IsOrderManager = await _userManager.IsInRoleAsync(user, "OrderManager");
            ViewBag.IsOwner = await _userManager.IsInRoleAsync(user, "Owner");
            var totalOrders = await _salesDb.Orders.CountAsync();
            var totalUsers = await _userManager.Users.CountAsync();
            var totalProducts = await _catalogDb.Products.CountAsync() + await _catalogDb.SweetItems.CountAsync();
            var totalRevenue = await _salesDb.Orders
                .Where(o => o.PaymentStatus == PaymentStatus.Succeeded)
                .SumAsync(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot);
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.TotalRevenue = totalRevenue;
            var today = DateTime.UtcNow.Date;
            var todayOrders = await _salesDb.Orders.CountAsync(o => o.CreatedAt.Date == today);
            var todayRevenue = await _salesDb.Orders
                .Where(o => o.CreatedAt.Date == today && o.PaymentStatus == PaymentStatus.Succeeded)
                .SumAsync(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot);
            ViewBag.TodayOrders = todayOrders;
            ViewBag.TodayRevenue = todayRevenue;
            ViewBag.UnreadMessages = await _salesDb.ContactMessages.CountAsync(m => !m.IsRead);
            ViewBag.AwaitingOrders = await _salesDb.Orders.CountAsync(o => o.OrderStatus == OrderStatus.AwaitingReview);
            ViewBag.PendingPayments = await _salesDb.Orders.CountAsync(o => o.OrderStatus == OrderStatus.PendingPayment);
            var recentOrders = await _salesDb.Orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();
            var userIds = recentOrders.Where(o => !string.IsNullOrEmpty(o.UserId)).Select(o => o.UserId).Distinct().ToList();
            var users = new Dictionary<string, string>();
            if (userIds.Any())
            {
                users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "نامشخص");
            }
            ViewBag.RecentOrders = recentOrders;
            ViewBag.UserNames = users;
            ViewBag.RecentMessages = await _salesDb.ContactMessages
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .ToListAsync();
            var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.UtcNow.Date.AddDays(-i)).Reverse().ToList();
            var salesData = new List<decimal>();
            var labels = new List<string>();

            foreach (var day in last7Days)
            {
                var dayRevenue = await _salesDb.Orders
                    .Where(o => o.CreatedAt.Date == day && o.PaymentStatus == PaymentStatus.Succeeded)
                    .SumAsync(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot);
                salesData.Add(dayRevenue);
                labels.Add(new PersianDateTime(day).ToString("MM/dd"));
            }
            ViewBag.SalesLabels = labels;
            ViewBag.SalesData = salesData;
            ViewBag.AwaitingCount = await _salesDb.Orders.CountAsync(o => o.OrderStatus == OrderStatus.AwaitingReview);
            ViewBag.PendingCount = await _salesDb.Orders.CountAsync(o => o.OrderStatus == OrderStatus.PendingPayment);
            ViewBag.PaidCount = await _salesDb.Orders.CountAsync(o => o.OrderStatus == OrderStatus.Paid);
            ViewBag.PreparingCount = await _salesDb.Orders.CountAsync(o => o.OrderStatus == OrderStatus.Preparing);
            ViewBag.DeliveredCount = await _salesDb.Orders.CountAsync(o => o.OrderStatus == OrderStatus.Delivered);

            return View();
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> Orders(string status = "all")
        {
            var query = _salesDb.Orders
                .Where(o => o.Notes != "WalletRecharge" && (o.Notes == null || !o.Notes.StartsWith("CustomCakeOrder_")))
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .AsQueryable();

            switch (status)
            {
                case "awaiting": query = query.Where(o => o.OrderStatus == OrderStatus.AwaitingReview); break;
                case "pending": query = query.Where(o => o.OrderStatus == OrderStatus.PendingPayment); break;
                case "paid": query = query.Where(o => o.OrderStatus == OrderStatus.Paid); break;
                case "preparing": query = query.Where(o => o.OrderStatus == OrderStatus.Preparing); break;
                case "delivered": query = query.Where(o => o.OrderStatus == OrderStatus.Delivered); break;
            }

            var orders = await query.ToListAsync();
            var userIds = orders.Where(o => !string.IsNullOrEmpty(o.UserId)).Select(o => o.UserId).Distinct().ToList();
            var users = new Dictionary<string, string>();
            if (userIds.Any())
            {
                users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "نامشخص");
            }
            ViewBag.UserNames = users;
            ViewBag.TotalOrders = orders.Count;
            ViewBag.TotalPayments = orders.Sum(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot);
            ViewBag.CurrentStatus = status;
            return View(orders);
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> DeleteProductItem(int itemId)
        {
            var item = await _salesDb.OrderItems.FindAsync(itemId);
            if (item == null) return NotFound();
            _salesDb.OrderItems.Remove(item);
            await _salesDb.SaveChangesAsync();
            TempData["SuccessMessage"] = "محصول با موفقیت حذف شد.";
            return RedirectToAction("OrderDetails", new { id = item.OrderId });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> ExportOrdersToExcel(string status = "all")
        {
            var query = _salesDb.Orders
                .Where(o => o.Notes != "WalletRecharge" && (o.Notes == null || !o.Notes.StartsWith("CustomCakeOrder_")))
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .AsQueryable();

            switch (status)
            {
                case "awaiting": query = query.Where(o => o.OrderStatus == OrderStatus.AwaitingReview); break;
                case "pending": query = query.Where(o => o.OrderStatus == OrderStatus.PendingPayment); break;
                case "paid": query = query.Where(o => o.OrderStatus == OrderStatus.Paid); break;
                case "preparing": query = query.Where(o => o.OrderStatus == OrderStatus.Preparing); break;
                case "delivered": query = query.Where(o => o.OrderStatus == OrderStatus.Delivered); break;
            }

            var orders = await query.ToListAsync();
            var userIds = orders.Where(o => !string.IsNullOrEmpty(o.UserId)).Select(o => o.UserId).Distinct().ToList();
            var users = new Dictionary<string, string>();
            if (userIds.Any())
            {
                users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "نامشخص");
            }

            var html = new StringBuilder();
            html.AppendLine("<html dir='rtl'><head><meta charset='UTF-8'><title>لیست سفارشات</title></head><body>");
            html.AppendLine("<table border='1' cellpadding='5' cellspacing='0' style='border-collapse: collapse;'>");
            html.AppendLine("<thead><tr style='background-color:#f2f2f2;'>");
            html.AppendLine("<th>کد سفارش</th><th>نام کاربر</th><th>گیرنده سفارش</th><th>قیمت نهایی</th><th>وزن نهایی</th><th>وضعیت</th><th>تاریخ</th>");
            html.AppendLine("</tr></thead><tbody>");

            foreach (var order in orders)
            {
                string userName = (order.UserId != null && users.ContainsKey(order.UserId)) ? users[order.UserId] : "نامشخص";
                string finalPrice = order.FinalTotalAmount.HasValue ? order.FinalTotalAmount.Value.ToString("N0") + " تومان" : "-";
                string finalWeight = order.FinalTotalWeightGrams.HasValue ? order.FinalTotalWeightGrams.Value.ToString() + " گرم" : "-";
                string statusText = order.OrderStatus.ToFarsi();
                string date = new PersianDateTime(order.CreatedAt).ToString("yyyy/MM/dd");

                html.AppendLine("<tr>");
                html.AppendLine($"<td>{order.OrderCode}</td>");
                html.AppendLine($"<td>{userName}</td>");
                html.AppendLine($"<td>{order.CustomerName}</td>");
                html.AppendLine($"<td>{finalPrice}</td>");
                html.AppendLine($"<td>{finalWeight}</td>");
                html.AppendLine($"<td>{statusText}</td>");
                html.AppendLine($"<td>{date}</td>");
                html.AppendLine("</tr>");
            }

            int totalOrders = orders.Count;
            decimal totalPayments = orders.Sum(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot);
            html.AppendLine("<tr style='background-color:#e6f7ff; font-weight:bold;'>");
            html.AppendLine($"<td colspan='2'>تعداد سفارشات: {totalOrders}</td>");
            html.AppendLine($"<td colspan='2'>جمع کل پرداخت‌ها: {totalPayments.ToString("N0")} تومان</td>");
            html.AppendLine("<td colspan='3'></td>");
            html.AppendLine("</tr></tbody></table></body></html>");

            var bytes = Encoding.UTF8.GetBytes(html.ToString());
            return File(bytes, "application/vnd.ms-excel", $"Orders_{status}_{DateTime.Now:yyyyMMdd_HHmmss}.xls");
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> UpdateBoxWeight(int orderId, string boxTitle, int finalWeightGrams, decimal finalPrice, string? adminNotes)
        {
            var existing = await _salesDb.BoxFinalInfos
                .FirstOrDefaultAsync(b => b.OrderId == orderId && b.BoxTitle == boxTitle);

            if (existing != null)
            {
                existing.FinalWeightGrams = finalWeightGrams;
                existing.FinalPrice = finalPrice;
                existing.AdminNotes = adminNotes;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _salesDb.BoxFinalInfos.Add(new BoxFinalInfo
                {
                    OrderId = orderId,
                    BoxTitle = boxTitle,
                    FinalWeightGrams = finalWeightGrams,
                    FinalPrice = finalPrice,
                    AdminNotes = adminNotes,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _salesDb.SaveChangesAsync();

            var allBoxInfos = await _salesDb.BoxFinalInfos.Where(b => b.OrderId == orderId).ToListAsync();
            var totalWeight = allBoxInfos.Sum(b => b.FinalWeightGrams);
            var totalPrice = allBoxInfos.Sum(b => b.FinalPrice);

            var order = await _salesDb.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.FinalTotalWeightGrams = totalWeight;
                order.FinalTotalAmount = totalPrice;
                order.IsPaymentEnabled = true;
                order.OrderStatus = OrderStatus.PendingPayment;
                order.PaymentStatus = PaymentStatus.Unpaid;
                order.UpdatedAt = DateTime.UtcNow;
                await _salesDb.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"وزن و قیمت نهایی جعبه {boxTitle} ثبت شد. کاربر می‌تواند پس از ورود به پنل خود، سفارش را پرداخت کند.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _salesDb.Orders
                .Include(o => o.Items)
                .Include(o => o.CustomerAddress)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            var boxInfos = await _salesDb.BoxFinalInfos
                .Where(b => b.OrderId == id)
                .ToDictionaryAsync(b => b.BoxTitle, b => b);
            ViewBag.BoxInfos = boxInfos;

            var sweetItemIds = order.Items.Where(x => x.SweetItemId.HasValue).Select(x => x.SweetItemId!.Value).Distinct().ToList();
            var sweetNames = sweetItemIds.Any()
                ? await _catalogDb.SweetItems.Where(x => sweetItemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.TitleFa)
                : new Dictionary<int, string>();

            var productIds = order.Items.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();
            var productNames = productIds.Any()
                ? await _catalogDb.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.TitleFa)
                : new Dictionary<int, string>();

            var allSweetItems = await _catalogDb.SweetItems
                .Where(x => x.IsActive)
                .Include(x => x.Category)
                .OrderBy(x => x.Category == null ? 0 : x.Category.SortOrder)
                .ThenBy(x => x.SortOrder)
                .ToListAsync();

            var sweetListForJs = allSweetItems
                .GroupBy(x => x.Category?.TitleFa ?? "دسته‌بندی نشده")
                .Select(g => new
                {
                    category = g.Key,
                    items = g.Select(s => new { s.Id, s.TitleFa, s.ApproxWeightGrams, s.PricePerKg })
                });

            ViewBag.SweetNames = sweetNames;
            ViewBag.ProductNames = productNames;
            ViewBag.SweetListJson = JsonSerializer.Serialize(sweetListForJs);
            ViewBag.BoxTypeTitle = order.Items.FirstOrDefault()?.BoxTitle ?? "نوع جعبه مشخص نیست";

            return View(order);
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> DeleteOrderItem(int itemId)
        {
            var item = await _salesDb.OrderItems.FindAsync(itemId);
            if (item == null) return NotFound();
            _salesDb.OrderItems.Remove(item);
            await _salesDb.SaveChangesAsync();
            return Ok(new { success = true, message = "آیتم با موفقیت حذف شد." });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> UpdateOrderItem(int itemId, int newSweetItemId, int newQuantity)
        {
            var item = await _salesDb.OrderItems.FindAsync(itemId);
            if (item == null) return NotFound();

            var sweetItem = await _catalogDb.SweetItems.FindAsync(newSweetItemId);
            if (sweetItem == null) return BadRequest("شیرینی نامعتبر است.");

            var rowPrice = (sweetItem.PricePerKg * sweetItem.ApproxWeightGrams) / 1000m;
            item.SweetItemId = newSweetItemId;
            item.Quantity = newQuantity;
            item.UnitPriceSnapshot = rowPrice;
            item.TotalPriceSnapshot = rowPrice * newQuantity;
            item.WeightSnapshotGrams = sweetItem.ApproxWeightGrams * newQuantity;

            await _salesDb.SaveChangesAsync();
            return Ok(new { success = true, message = "آیتم با موفقیت به‌روزرسانی شد." });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> UpdateOrderWeight(int orderId, int finalWeightGrams, decimal finalPrice, string? adminNotes)
        {
            var order = await _salesDb.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            order.FinalTotalWeightGrams = finalWeightGrams;
            order.FinalTotalAmount = finalPrice;
            order.AdminNotes = adminNotes;
            order.IsPaymentEnabled = true;
            order.OrderStatus = OrderStatus.PendingPayment;
            order.PaymentStatus = PaymentStatus.Unpaid;
            order.UpdatedAt = DateTime.UtcNow;

            await _salesDb.SaveChangesAsync();
            TempData["SuccessMessage"] = "وزن و قیمت نهایی ثبت شد. کاربر می‌تواند سفارش را پرداخت کند.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus status)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            var oldStatus = order.OrderStatus;
            order.OrderStatus = status;
            order.UpdatedAt = DateTime.UtcNow;
            await _salesDb.SaveChangesAsync();

            // Note: inventory is now deducted only in PaymentController.Callback after successful payment
            // No double deduction here

            if (status == OrderStatus.Delivered && oldStatus != OrderStatus.Delivered && order.PaymentStatus == PaymentStatus.Succeeded)
                await ApplyCashbackToWallet(order);

            TempData["SuccessMessage"] = $"وضعیت سفارش به {status.ToFarsi()} تغییر کرد.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        private async Task ApplyCashbackToWallet(Order order)
        {
            var settings = await _salesDb.WalletSettings.FirstOrDefaultAsync();
            if (settings == null || !settings.IsEnabled) return;

            decimal cashbackAmount = 0;
            if (settings.ReturnType == "Percentage")
                cashbackAmount = (order.TotalAmountSnapshot * settings.ReturnValue) / 100;
            else if (settings.ReturnType == "Fixed")
                cashbackAmount = settings.ReturnValue;

            if (cashbackAmount <= 0) return;

            if (order.TotalAmountSnapshot >= settings.MinimumOrderAmount)
            {
                var wallet = await _salesDb.Wallets.FirstOrDefaultAsync(w => w.UserId == order.UserId);
                if (wallet == null)
                {
                    wallet = new Wallet { UserId = order.UserId, Balance = 0 };
                    _salesDb.Wallets.Add(wallet);
                }

                if (settings.MaxWalletBalance > 0 && wallet.Balance + cashbackAmount > settings.MaxWalletBalance)
                    cashbackAmount = settings.MaxWalletBalance - wallet.Balance;

                if (cashbackAmount > 0)
                {
                    wallet.Balance += cashbackAmount;
                    wallet.UpdatedAt = DateTime.UtcNow;
                    _salesDb.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = order.UserId,
                        Amount = cashbackAmount,
                        Type = "Cashback",
                        Description = $"بازگشت وجه از سفارش #{order.OrderCode}",
                        OrderId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _salesDb.SaveChangesAsync();
                }
            }
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> DeleteBox(int orderId, string boxTitle)
        {
            var items = await _salesDb.OrderItems.Where(x => x.OrderId == orderId && x.BoxTitle == boxTitle).ToListAsync();
            if (!items.Any()) return NotFound();
            _salesDb.OrderItems.RemoveRange(items);
            await _salesDb.SaveChangesAsync();
            TempData["SuccessMessage"] = $"جعبه {boxTitle} با موفقیت حذف شد.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> DeleteOrder(int orderId)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();
            _salesDb.OrderItems.RemoveRange(order.Items);
            _salesDb.Orders.Remove(order);
            await _salesDb.SaveChangesAsync();
            TempData["SuccessMessage"] = "سفارش با موفقیت حذف شد.";
            return RedirectToAction("Orders");
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> EditOrderItem(int itemId)
        {
            var item = await _salesDb.OrderItems.FindAsync(itemId);
            if (item == null) return NotFound();
            ViewBag.SweetItems = await _catalogDb.SweetItems
                .Where(x => x.IsActive)
                .Include(x => x.Category)
                .OrderBy(x => x.Category == null ? 0 : x.Category.SortOrder)
                .ThenBy(x => x.SortOrder)
                .ToListAsync();
            ViewBag.ItemId = itemId;
            ViewBag.OrderId = item.OrderId;
            return View();
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        public async Task<IActionResult> EditOrderItem(int itemId, int newSweetItemId)
        {
            var item = await _salesDb.OrderItems.FindAsync(itemId);
            if (item == null) return NotFound();

            var sweetItem = await _catalogDb.SweetItems.FindAsync(newSweetItemId);
            if (sweetItem == null) return BadRequest("شیرینی نامعتبر است.");

            var rowPrice = (sweetItem.PricePerKg * sweetItem.ApproxWeightGrams) / 1000m;
            item.SweetItemId = newSweetItemId;
            item.UnitPriceSnapshot = rowPrice;
            item.TotalPriceSnapshot = rowPrice;
            item.WeightSnapshotGrams = sweetItem.ApproxWeightGrams;

            await _salesDb.SaveChangesAsync();
            TempData["SuccessMessage"] = "شیرینی با موفقیت تغییر کرد.";
            return RedirectToAction("OrderDetails", new { id = item.OrderId });
        }
        [Authorize(Roles = "Admin,Owner")]
        [HttpGet]
        public async Task<IActionResult> GetSweetItemsList()
        {
            var sweetItems = await _catalogDb.SweetItems
                .Where(x => x.IsActive)
                .Include(x => x.Category)
                .OrderBy(x => x.Category == null ? 0 : x.Category.SortOrder)
                .ThenBy(x => x.SortOrder)
                .Select(x => new { x.Id, x.TitleFa, x.ApproxWeightGrams, x.PricePerKg, CategoryTitle = x.Category != null ? x.Category.TitleFa : "دسته‌بندی نشده" })
                .ToListAsync();

            return Ok(sweetItems.GroupBy(x => x.CategoryTitle).Select(g => new { Category = g.Key, Items = g.Select(s => new { s.Id, s.TitleFa, s.ApproxWeightGrams, s.PricePerKg }) }));
        }
        [Authorize(Roles = "Admin,Owner")]
        [HttpGet]
        public IActionResult Settings()
        {
            return View();
        }
        [Authorize(Roles = "Admin,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(int freeDeliveryThreshold, int walletCashbackPercent)
        {
            // TODO: Persist these settings to SiteSetting or a dedicated settings table
            await Task.CompletedTask;
            TempData["SuccessMessage"] = "تنظیمات با موفقیت ذخیره شد.";
            return RedirectToAction("Settings");
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> CustomCakeOrders()
        {
            var orders = await _salesDb.CustomCakeOrders.OrderByDescending(o => o.CreatedAt).ToListAsync();
            return View(orders);
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> CustomCakeOrderDetails(int id)
        {
            var order = await _salesDb.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCustomCakeOrder(int id, CustomCakeOrderStatus status, decimal? finalPrice, string? adminNotes)
        {
            var order = await _salesDb.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = status;
            order.FinalPrice = finalPrice;
            order.AdminNotes = adminNotes;
            order.UpdatedAt = DateTime.UtcNow;

            await _salesDb.SaveChangesAsync();
            TempData["SuccessMessage"] = "وضعیت سفارش کیک به‌روزرسانی شد.";
            return RedirectToAction("CustomCakeOrders");
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> EditCustomCakeOrder(int id)
        {
            var order = await _salesDb.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomCakeOrder(int id, CustomCakeOrder model,
            string? desiredDeliveryDatePersian,
            string? desiredDeliveryTime)
        {
            if (id != model.Id) return NotFound();
            var order = await _salesDb.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            ModelState.Remove("UserId");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("IsPaid");
            ModelState.Remove("Status");
            ModelState.Remove("FinalPrice");
            ModelState.Remove("AdminNotes");
            ModelState.Remove("UpdatedAt");

            if (string.IsNullOrWhiteSpace(model.Flavor))
                ModelState.AddModelError("Flavor", "لطفاً طعم کیک را وارد کنید.");
            if (string.IsNullOrWhiteSpace(model.Shape))
                ModelState.AddModelError("Shape", "لطفاً شکل کیک را وارد کنید.");
            if (!model.WeightGrams.HasValue || model.WeightGrams.Value < 500)
                ModelState.AddModelError("WeightGrams", "وزن تقریبی باید حداقل ۵۰۰ گرم باشد.");
            if (!model.Servings.HasValue || model.Servings.Value < 1)
                ModelState.AddModelError("Servings", "تعداد نفرات را وارد کنید.");

            if (!string.IsNullOrEmpty(desiredDeliveryDatePersian) && !string.IsNullOrEmpty(desiredDeliveryTime))
            {
                var deliveryDateTime = PersianDateHelper.ConvertPersianToDateTime(desiredDeliveryDatePersian, desiredDeliveryTime);
                if (deliveryDateTime.HasValue)
                    order.DesiredDeliveryDateTime = deliveryDateTime.Value;
                else
                    ModelState.AddModelError("DesiredDeliveryDateTime", "تاریخ یا ساعت تحویل نامعتبر است.");
            }
            else
            {
                order.DesiredDeliveryDateTime = null;
            }
            order.WeightGrams = model.WeightGrams;
            order.Servings = model.Servings;
            order.Flavor = model.Flavor;
            order.Shape = model.Shape;
            order.Ingredients = model.Ingredients;
            order.Occasion = model.Occasion;
            order.SpecialRequests = model.SpecialRequests;
            order.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(model.SampleImagePath))
            {
                order.SampleImagePath = model.SampleImagePath;
            }
            if (!string.IsNullOrWhiteSpace(model.PrintImagePath))
            {
                order.PrintImagePath = model.PrintImagePath;
            }
            if (ModelState.IsValid)
            {
                await _salesDb.SaveChangesAsync();
                TempData["SuccessMessage"] = "سفارش کیک با موفقیت ویرایش شد.";
                return RedirectToAction("CustomCakeOrders");
            }

            return View(order);
        }
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomCakeOrder(int id)
        {
            var order = await _salesDb.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != CustomCakeOrderStatus.Pending)
            {
                TempData["ErrorMessage"] = "فقط سفارشات در انتظار بررسی قابل حذف هستند.";
                return RedirectToAction("CustomCakeOrders");
            }
            _salesDb.CustomCakeOrders.Remove(order);
            await _salesDb.SaveChangesAsync();

            TempData["SuccessMessage"] = "سفارش کیک با موفقیت حذف شد.";
            return RedirectToAction("CustomCakeOrders");
        }
    }
}