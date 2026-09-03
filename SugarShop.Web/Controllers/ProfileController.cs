using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;
using SugarShop.Web.Services;
using SugarShop.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProfileController(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }
        private List<string> GetDefaultAvatars()
        {
            var defaultAvatarPath = "/images/avatars/default/";
            var list = new List<string>();
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/avatars/default");
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "*.*")
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".gif")))
                {
                    list.Add(defaultAvatarPath + Path.GetFileName(file));
                }
            }
            if (!list.Any())
                list.AddRange(new[] { "/images/avatars/default/avatar1.png", "/images/avatars/default/avatar2.png", "/images/avatars/default/avatar3.png" });
            return list;
        }
        private async Task<string?> SaveFile(IFormFile file, string prefix)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images/cake_orders");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{prefix}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/cake_orders/{uniqueFileName}";
        }


        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId) as ApplicationUser;

            var orders = await _salesDb.Orders
                .Where(o => o.UserId == userId && o.Notes != "WalletRecharge")
                .ToListAsync();

            int totalOrders = orders.Count;
            int pendingPaymentOrders = orders.Count(o => o.OrderStatus == OrderStatus.PendingPayment);
            int deliveredOrders = orders.Count(o => o.OrderStatus == OrderStatus.Delivered);

            var wallet = await _salesDb.Set<Wallet>().FirstOrDefaultAsync(w => w.UserId == userId);
            decimal walletBalance = wallet?.Balance ?? 0;

            int openTickets = await _salesDb.Set<Ticket>()
                .Where(t => t.UserId == userId && t.Status != TicketStatus.Answered && t.Status != TicketStatus.Closed)
                .CountAsync();

            var lastOrder = await _salesDb.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            var defaultAddress = await _salesDb.Addresses
                .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault);

            var model = new DashboardViewModel
            {
                TotalOrders = totalOrders,
                PendingPaymentOrders = pendingPaymentOrders,
                DeliveredOrders = deliveredOrders,
                WalletBalance = walletBalance,
                OpenTickets = openTickets,
                LastOrder = lastOrder,
                FullName = user?.FullName ?? "",
                Email = user?.Email ?? "",
                PhoneNumber = user?.PhoneNumber ?? "",
                AvatarPath = user?.AvatarPath,
                BirthDate = user?.BirthDate,
                Gender = user?.Gender,
                DefaultAddress = defaultAddress
            };

            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User) as ApplicationUser;
            if (user == null) return NotFound();

            var defaultAvatars = GetDefaultAvatars();
            ViewBag.DefaultAvatars = defaultAvatars;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ApplicationUser model, IFormFile? avatarFile, string? selectedAvatar)
        {
            var user = await _userManager.GetUserAsync(User) as ApplicationUser;
            if (user == null) return NotFound();

            var originalFullName = user.FullName;
            var originalPhoneNumber = user.PhoneNumber;

            user.FullName = string.IsNullOrWhiteSpace(model.FullName) ? originalFullName : model.FullName;
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? originalPhoneNumber : model.PhoneNumber;
            user.BirthDate = model.BirthDate;
            user.Gender = model.Gender;
            user.NationalCode = string.IsNullOrWhiteSpace(model.NationalCode) ? null : model.NationalCode.Trim();

            if (avatarFile != null && avatarFile.Length > 0)
            {
                if (!ImageUploadValidator.IsValidImage(avatarFile, ImageUploadValidator.MaxAvatarBytes, out var avatarError))
                {
                    ModelState.AddModelError("avatarFile", avatarError!);
                }
                else
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/avatars");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);
                    var filePath = Path.Combine(uploadPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await avatarFile.CopyToAsync(stream);

                    if (!string.IsNullOrEmpty(user.AvatarPath) && !user.AvatarPath.StartsWith("/images/avatars/default/"))
                    {
                        var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.AvatarPath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                    }
                    user.AvatarPath = "/images/avatars/" + fileName;
                }
            }
            else if (!string.IsNullOrEmpty(selectedAvatar))
            {
                user.AvatarPath = selectedAvatar;
            }

            if (ModelState.IsValid)
            {
                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["Success"] = "پروفایل با موفقیت به‌روزرسانی شد.";
                    return RedirectToAction(nameof(Index));
                }
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            ViewBag.DefaultAvatars = GetDefaultAvatars();
            return View(model);
        }
        public async Task<IActionResult> Orders()
        {
            var userId = _userManager.GetUserId(User);
            var orders = await _salesDb.Orders
                .Where(o => o.UserId == userId
                    && o.Notes != "WalletRecharge"
                    && (o.Notes == null || !o.Notes.StartsWith("CustomCakeOrder_")))
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var orderIds = orders.Select(o => o.Id).ToList();
            var boxInfos = await _salesDb.BoxFinalInfos
                .Where(b => orderIds.Contains(b.OrderId))
                .ToListAsync();

            var boxInfosByOrder = boxInfos
                .GroupBy(b => b.OrderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<OrderListItemViewModel>();
            foreach (var order in orders)
            {
                decimal boxFinalTotal = 0;
                if (boxInfosByOrder.ContainsKey(order.Id))
                {
                    boxFinalTotal = boxInfosByOrder[order.Id].Sum(b => b.FinalPrice);
                }

                decimal productTotal = order.Items
                    .Where(i => i.ItemType == OrderItemType.Product)
                    .Sum(i => i.TotalPriceSnapshot);

                // ✅ اصلاح: اضافه کردن هزینه پیک
                decimal totalFinalPrice = boxFinalTotal + productTotal + order.DeliveryFeeSnapshot;

                bool isDeletable = order.OrderStatus == OrderStatus.AwaitingReview;

                result.Add(new OrderListItemViewModel
                {
                    Id = order.Id,
                    OrderCode = order.OrderCode,
                    OrderStatus = order.OrderStatus,
                    PaymentStatus = order.PaymentStatus,
                    CreatedAt = order.CreatedAt,
                    TotalFinalPrice = totalFinalPrice,  // ✅ حالا شامل هزینه پیک هم هست
                    IsPaymentEnabled = order.IsPaymentEnabled,
                    IsDeletable = isDeletable
                });
            }
            return View(result);
        }
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (order == null) return NotFound();
            if (order.Notes == "WalletRecharge")
            {
                return RedirectToAction(nameof(Orders));
            }

            var sweetItemIds = order.Items
                .Where(x => x.SweetItemId.HasValue)
                .Select(x => x.SweetItemId.Value)
                .Distinct();
            var sweetNames = await _catalogDb.SweetItems
                .Where(x => sweetItemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.TitleFa);

            var productIds = order.Items
                .Where(x => x.ProductId.HasValue)
                .Select(x => x.ProductId.Value)
                .Distinct();
            var productNames = await _catalogDb.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.TitleFa);

            var boxInfos = await _salesDb.BoxFinalInfos
                .Where(b => b.OrderId == id)
                .ToDictionaryAsync(b => b.BoxTitle, b => b);

            ViewBag.SweetNames = sweetNames;
            ViewBag.ProductNames = productNames;
            ViewBag.BoxInfos = boxInfos;

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return NotFound();

            if (order.OrderStatus != OrderStatus.AwaitingReview)
            {
                TempData["Error"] = "این سفارش قابل حذف نیست.";
                return RedirectToAction(nameof(Orders));
            }

            // بازگشت وجه کیف پول (سفارش در انتظار بررسی = پرداخت‌نشده) — idempotent
            await OrderWalletHelper.RefundWalletAsync(_salesDb, order);

            var boxInfos = await _salesDb.BoxFinalInfos.Where(b => b.OrderId == orderId).ToListAsync();
            _salesDb.BoxFinalInfos.RemoveRange(boxInfos);
            _salesDb.OrderItems.RemoveRange(order.Items);
            _salesDb.Orders.Remove(order);
            await _salesDb.SaveChangesAsync();

            TempData["SuccessMessage"] = "سفارش با موفقیت حذف شد.";
            return RedirectToAction(nameof(Orders));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayOrder(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return NotFound();

            if (!order.IsPaymentEnabled || order.PaymentStatus != PaymentStatus.Unpaid)
            {
                TempData["Error"] = "پرداخت برای این سفارش امکان‌پذیر نیست.";
                return RedirectToAction("OrderDetails", new { id = orderId });
            }

            return RedirectToAction("RequestPayment", "Payment", new { orderId = order.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Invoice(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return NotFound();

            // فاکتور فقط پس از پرداخت موفق سفارش صادر/نمایش داده می‌شود
            if (order.PaymentStatus != PaymentStatus.Succeeded)
            {
                TempData["Error"] = "فاکتور پس از پرداخت موفق سفارش صادر می‌شود.";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            // شارژ کیف پول و سفارش‌های موقت کیک ردیف فروش ندارند؛ فاکتور فروش برای آن‌ها صادر نمی‌شود
            if (order.Notes == "WalletRecharge" ||
                (order.Notes != null && order.Notes.StartsWith("CustomCakeOrder_")))
            {
                TempData["Error"] = "برای این سفارش فاکتور فروش صادر نمی‌شود.";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            var invoice = await InvoiceService.GetOrCreateForOrderAsync(_salesDb, _catalogDb, orderId);
            return View("ViewInvoice", invoice);
        }

        public async Task<IActionResult> CustomCakeOrders()
        {
            var userId = _userManager.GetUserId(User);
            var orders = await _salesDb.CustomCakeOrders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> CustomCakeOrderDetails(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.CustomCakeOrders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> EditCustomCakeOrder(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.CustomCakeOrders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (order == null || order.Status != CustomCakeOrderStatus.Pending)
                return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomCakeOrder(int id,
                                                             CustomCakeOrder model,
                                                             string? desiredDeliveryDatePersian,
                                                             string? desiredDeliveryTime,
                                                             IFormFile? sampleImage,
                                                             IFormFile? printImage)
        {
            if (id != model.Id) return NotFound();

            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.CustomCakeOrders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (order == null || order.Status != CustomCakeOrderStatus.Pending)
                return NotFound();

            ModelState.Remove("UserId");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("IsPaid");
            ModelState.Remove("Status");
            ModelState.Remove("FinalPrice");
            ModelState.Remove("AdminNotes");
            ModelState.Remove("UpdatedAt");

            // اعتبارسنجی فیلدهای اجباری
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
            if (sampleImage != null && sampleImage.Length > 0)
            {
                if (!ImageUploadValidator.IsValidImage(sampleImage, ImageUploadValidator.MaxCakeImageBytes, out var sampleImageError))
                {
                    ModelState.AddModelError("sampleImage", sampleImageError!);
                }
                else
                {
                    if (!string.IsNullOrEmpty(order.SampleImagePath))
                    {
                        var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, order.SampleImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }
                    order.SampleImagePath = await SaveFile(sampleImage, "cake_sample");
                }
            }
            if (printImage != null && printImage.Length > 0)
            {
                if (!ImageUploadValidator.IsValidImage(printImage, ImageUploadValidator.MaxCakeImageBytes, out var printImageError))
                {
                    ModelState.AddModelError("printImage", printImageError!);
                }
                else
                {
                    if (!string.IsNullOrEmpty(order.PrintImagePath))
                    {
                        var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, order.PrintImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }
                    order.PrintImagePath = await SaveFile(printImage, "cake_print");
                }
            }

            if (ModelState.IsValid)
            {
                await _salesDb.SaveChangesAsync();
                TempData["Success"] = "سفارش شما با موفقیت ویرایش شد.";
                return RedirectToAction("CustomCakeOrders");
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomCakeOrder(int id)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _salesDb.CustomCakeOrders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (order == null) return NotFound();
            if (order.Status != CustomCakeOrderStatus.Pending)
            {
                TempData["Error"] = "این سفارش قابل حذف نیست.";
                return RedirectToAction("CustomCakeOrders");
            }
            if (!string.IsNullOrEmpty(order.SampleImagePath))
            {
                var samplePath = Path.Combine(_webHostEnvironment.WebRootPath, order.SampleImagePath.TrimStart('/'));
                if (System.IO.File.Exists(samplePath))
                    System.IO.File.Delete(samplePath);
            }
            if (!string.IsNullOrEmpty(order.PrintImagePath))
            {
                var printPath = Path.Combine(_webHostEnvironment.WebRootPath, order.PrintImagePath.TrimStart('/'));
                if (System.IO.File.Exists(printPath))
                    System.IO.File.Delete(printPath);
            }

            _salesDb.CustomCakeOrders.Remove(order);
            await _salesDb.SaveChangesAsync();

            TempData["Success"] = "سفارش کیک با موفقیت حذف شد.";
            return RedirectToAction("CustomCakeOrders");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayCustomCakeOrder(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cakeOrder = await _salesDb.CustomCakeOrders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (cakeOrder == null || cakeOrder.Status != CustomCakeOrderStatus.Accepted || cakeOrder.IsPaid)
                return BadRequest();
            var tempOrder = new Order
            {
                OrderCode = "CAKE-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                UserId = userId,
                OrderStatus = OrderStatus.PendingPayment,
                PaymentStatus = PaymentStatus.Unpaid,
                TotalAmountSnapshot = cakeOrder.FinalPrice.Value,
                FinalTotalAmount = cakeOrder.FinalPrice.Value,
                DeliveryFeeSnapshot = 0,
                CustomerName = User.Identity?.Name ?? "کاربر",
                CreatedAt = DateTime.UtcNow,
                IsPaymentEnabled = true,
                Notes = $"CustomCakeOrder_{cakeOrder.Id}"
            };
            _salesDb.Orders.Add(tempOrder);
            await _salesDb.SaveChangesAsync();
            return RedirectToAction("RequestPayment", "Payment", new { orderId = tempOrder.Id });
        }
        public async Task<IActionResult> Wallet()
        {
            var userId = _userManager.GetUserId(User);
            var wallet = await _salesDb.Set<Wallet>().FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0 };
                _salesDb.Set<Wallet>().Add(wallet);
                await _salesDb.SaveChangesAsync();
            }
            var transactions = await _salesDb.Set<WalletTransaction>()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            ViewBag.Transactions = transactions;
            return View(wallet);
        }
        public IActionResult ChangePassword()
        {
            return Redirect("/Identity/Account/Manage");
        }
        [HttpGet]
        public IActionResult Ticket()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ticket(string subject, string message)
        {
            var userId = _userManager.GetUserId(User);
            var ticket = new Ticket
            {
                UserId = userId,
                Subject = subject,
                Message = message,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow
            };
            _salesDb.Set<Ticket>().Add(ticket);
            await _salesDb.SaveChangesAsync();
            TempData["Success"] = "تیکت شما با موفقیت ثبت شد.";
            return RedirectToAction("Ticket");
        }

        public async Task<IActionResult> MyTickets()
        {
            var userId = _userManager.GetUserId(User);
            var tickets = await _salesDb.Set<Ticket>()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tickets);
        }
    }
}