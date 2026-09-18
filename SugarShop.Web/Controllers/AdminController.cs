using MD.PersianDateTime;
using ClosedXML.Excel;
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
using SugarShop.Web.Services;
using SugarShop.Web.ViewModels;
using QRCoder;
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
        private readonly SugarShop.Web.Services.Sms.SmsService _sms;
        private readonly SugarShop.Web.Services.OrderPricingService _pricingService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            InventoryService inventoryService,
            IWebHostEnvironment webHostEnvironment,
            UserManager<ApplicationUser> userManager,
            SugarShop.Web.Services.Sms.SmsService smsService,
            SugarShop.Web.Services.OrderPricingService pricingService,
            ILogger<AdminController> logger)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
            _inventoryService = inventoryService;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _sms = smsService;
            _pricingService = pricingService;
            _logger = logger;
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.IsAdmin = await _userManager.IsInRoleAsync(user!, "Admin");
            ViewBag.IsOrderManager = await _userManager.IsInRoleAsync(user!, "OrderManager");
            ViewBag.IsOwner = await _userManager.IsInRoleAsync(user!, "Owner");

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

            // ── آمار «امروز» بر مبنای روز ایران (نه روز UTC) ──
            var todayStartUtc = IranClock.DayStartUtc;
            var todayEndUtc = IranClock.DayEndUtc;

            var todayOrders = await _salesDb.Orders
                .CountAsync(o => o.CreatedAt >= todayStartUtc && o.CreatedAt < todayEndUtc);
            var todayRevenue = await _salesDb.Orders
                .Where(o => o.CreatedAt >= todayStartUtc && o.CreatedAt < todayEndUtc
                    && o.PaymentStatus == PaymentStatus.Succeeded)
                .SumAsync(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot);

            ViewBag.TodayOrders = todayOrders;
            ViewBag.TodayRevenue = todayRevenue;
            ViewBag.UnreadMessages = await _salesDb.ContactMessages.CountAsync(m => !m.IsRead);

            // شمارش وضعیت‌ها با یک کوئری گروه‌بندی‌شده (قبلاً ۷ کوئری جداگانه بود)
            var statusCounts = await _salesDb.Orders
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            int CountOf(OrderStatus status) => statusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

            ViewBag.AwaitingOrders = CountOf(OrderStatus.AwaitingReview);
            ViewBag.PendingPayments = CountOf(OrderStatus.PendingPayment);
            ViewBag.AwaitingCount = CountOf(OrderStatus.AwaitingReview);
            ViewBag.PendingCount = CountOf(OrderStatus.PendingPayment);
            ViewBag.PaidCount = CountOf(OrderStatus.Paid);
            ViewBag.PreparingCount = CountOf(OrderStatus.Preparing);
            ViewBag.DeliveredCount = CountOf(OrderStatus.Delivered);

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

            // ── نمودار ۷ روز گذشته: یک کوئری برای کل بازه و گروه‌بندی بر مبنای روز ایران ──
            var chartStartUtc = IranClock.DayStartUtcFor(IranClock.Today.AddDays(-6));
            var chartOrders = await _salesDb.Orders
                .Where(o => o.CreatedAt >= chartStartUtc && o.CreatedAt < todayEndUtc
                    && o.PaymentStatus == PaymentStatus.Succeeded)
                .Select(o => new { o.CreatedAt, Amount = o.FinalTotalAmount ?? o.TotalAmountSnapshot })
                .ToListAsync();

            var salesData = new List<decimal>();
            var labels = new List<string>();
            for (int i = 6; i >= 0; i--)
            {
                var iranDay = IranClock.Today.AddDays(-i);
                var dayStart = IranClock.DayStartUtcFor(iranDay);
                var dayEnd = IranClock.DayEndUtcFor(iranDay);
                salesData.Add(chartOrders.Where(o => o.CreatedAt >= dayStart && o.CreatedAt < dayEnd).Sum(o => o.Amount));
                labels.Add(new PersianDateTime(iranDay).ToString("MM/dd"));
            }

            ViewBag.SalesLabels = labels;
            ViewBag.SalesData = salesData;

            // ── 📊 آمار دانلود اپلیکیشن اندروید (یک کوئری تجمیعی) ──
            var monthStartUtc = IranClock.MonthStartUtc;
            var appStats = await _salesDb.AppDownloadLogs
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    ThisMonth = g.Count(d => d.DownloadedAt >= monthStartUtc),
                    Today = g.Count(d => d.DownloadedAt >= todayStartUtc && d.DownloadedAt < todayEndUtc),
                    Unique = g.Where(d => d.IpAddress != null).Select(d => d.IpAddress).Distinct().Count()
                })
                .FirstOrDefaultAsync();

            ViewBag.AppDownloadsTotal = appStats?.Total ?? 0;
            ViewBag.AppDownloadsThisMonth = appStats?.ThisMonth ?? 0;
            ViewBag.AppDownloadsToday = appStats?.Today ?? 0;
            ViewBag.AppDownloadsUnique = appStats?.Unique ?? 0;

            return View();
        }

        /// <summary>
        /// کوئری پایه لیست سفارشهای فروشگاه در پنل مدیریت.
        /// سفارشهای داخلی (شارژ کیف پول و سفارش موقت کیک سفارشی) از این لیست حذف می‌شوند و
        /// جست‌وجو کاملاً سمت سرور (SQL) انجام می‌شود تا با زیاد شدن تعداد سفارش‌ها
        /// هیچ ردیفی در حافظه بارگذاری نشود.
        /// </summary>
        private async Task<IQueryable<Order>> BuildAdminOrdersQueryAsync(string? search)
        {
            var query = _salesDb.Orders
                .AsNoTracking()
                .Where(o => o.Notes != OrderNotes.WalletRecharge
                            && (o.Notes == null || !o.Notes.StartsWith(OrderNotes.CustomCakeOrderPrefix)));

            var term = search?.Trim();
            if (string.IsNullOrWhiteSpace(term)) return query;

            // ارقام فارسی/عربی به ASCII تبدیل می‌شوند تا جست‌وجوی «۱۲۳۴» کد سفارش «1234» را هم پیدا کند
            term = term.ToEnglishNumber();
            if (term.Length > 100) term = term[..100];

            // نام/ایمیل کاربر در DbContext جداگانه Identity ذخیره می‌شود؛ پس اول شناسه کاربران منطبق را می‌گیریم
            // و سپس سفارش‌هایشان را هم در نتیجه جست‌وجو می‌آوریم (بدون JOIN بین دو DbContext).
            var matchedUserIds = await _userManager.Users
                .Where(u => (u.UserName != null && u.UserName.Contains(term))
                            || (u.Email != null && u.Email.Contains(term))
                            || u.FullName.Contains(term))
                .Select(u => u.Id)
                .Take(200)
                .ToListAsync();

            return query.Where(o => o.OrderCode.Contains(term)
                                    || o.CustomerName.Contains(term)
                                    || o.CustomerPhone.Contains(term)
                                    || (o.UserId != null && matchedUserIds.Contains(o.UserId)));
        }

        private static IQueryable<Order> ApplyAdminOrderStatusFilter(IQueryable<Order> query, string? status)
        {
            return status switch
            {
                "awaiting" => query.Where(o => o.OrderStatus == OrderStatus.AwaitingReview),
                "pending" => query.Where(o => o.OrderStatus == OrderStatus.PendingPayment),
                "paid" => query.Where(o => o.OrderStatus == OrderStatus.Paid),
                "preparing" => query.Where(o => o.OrderStatus == OrderStatus.Preparing),
                "delivered" => query.Where(o => o.OrderStatus == OrderStatus.Delivered),
                _ => query
            };
        }

        /// <summary>سقف ردیف‌های خروجی اکسل تا صادر کردن کل جدول، سرور را از پا نیندازد.</summary>
        private const int MaxExcelExportRows = 20000;

        /// <summary>فقط وضعیت‌های شناخته‌شده پذیرفته می‌شوند (این مقدار در نام فایل خروجی هم استفاده می‌شود).</summary>
        private static string NormalizeAdminOrderStatus(string? status) => status switch
        {
            "awaiting" or "pending" or "paid" or "preparing" or "delivered" => status,
            _ => "all"
        };

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> Orders(string status = "all", string? search = null, int page = 1)
        {
            const int pageSize = 20;
            status = NormalizeAdminOrderStatus(status);

            // کوئری پایه (بدون فیلتر وضعیت) تا شمارنده تب‌ها با همان عبارت جست‌وجو محاسبه شود
            var baseQuery = await BuildAdminOrdersQueryAsync(search);
            var query = ApplyAdminOrderStatusFilter(baseQuery, status);

            // تعداد و جمع مبلغ با یک کوئری تجمیعی در SQL محاسبه می‌شوند (نه با بارگذاری ردیف‌ها)
            var totalOrders = await query.CountAsync();
            var totalPayments = await query
                .SumAsync(o => (decimal?)(o.FinalTotalAmount ?? o.TotalAmountSnapshot)) ?? 0m;

            var totalPages = Math.Max(1, (int)Math.Ceiling(totalOrders / (double)pageSize));
            var currentPage = Math.Clamp(page, 1, totalPages);

            // ✅ فقط یک صفحه خوانده می‌شود (قبلاً همه سفارش‌ها همراه ردیف‌هایشان لود می‌شد)
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // نام کاربران فقط برای سفارش‌های همین صفحه از Identity خوانده می‌شود
            var userIds = orders
                .Where(o => !string.IsNullOrEmpty(o.UserId))
                .Select(o => o.UserId!)
                .Distinct()
                .ToList();
            var users = new Dictionary<string, string>();
            if (userIds.Count > 0)
            {
                users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "نامشخص");
            }

            var statusCounts = await baseQuery
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            ViewBag.UserNames = users;
            ViewBag.StatusCounts = statusCounts;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalPayments = totalPayments;
            ViewBag.CurrentStatus = status;
            ViewBag.Search = search?.Trim() ?? string.Empty;
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;

            return View(orders);
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductItem(int itemId)
        {
            var item = await _salesDb.OrderItems.FindAsync(itemId);
            if (item == null) return NotFound();

            _salesDb.OrderItems.Remove(item);
            await _salesDb.SaveChangesAsync();

            // مبلغ سفارش باید پس از حذف ردیف بازمحاسبه شود، وگرنه عدد نمایش‌داده‌شده با مبلغ دریافتی فرق می‌کند
            await RecalculateOrderTotalsAsync(item.OrderId);

            // اگر فاکتوری صادر شده، با وضعیت جدید سفارش همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, item.OrderId);

            TempData["SuccessMessage"] = "محصول با موفقیت حذف شد.";
            return RedirectToAction("OrderDetails", new { id = item.OrderId });
        }

        /// <summary>
        /// آماده‌سازی متن برای سلول‌های اکسل: کاراکترهای کنترلی که OpenXML قبول نمی‌کند حذف می‌شوند.
        /// مقادیر به‌صورت «متن» نوشته می‌شوند، پس رشته‌های شروع‌شده با = + - @ هرگز به‌عنوان فرمول اجرا نمی‌شوند
        /// (تزریق فرمول در خروجی اکسل واقعی ذاتاً بی‌اثر است).
        /// </summary>
        private static string ExcelText(string? value) => ExcelSafeText.Clean(value);

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> ExportOrdersToExcel(string status = "all", string? search = null)
        {
            status = NormalizeAdminOrderStatus(status);

            // خروجی اکسل دقیقاً همان چیزی است که در لیست پنل فیلتر شده (وضعیت + جست‌وجو)
            var query = ApplyAdminOrderStatusFilter(await BuildAdminOrdersQueryAsync(search), status);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Take(MaxExcelExportRows)
                .ToListAsync();

            if (orders.Count == MaxExcelExportRows)
                _logger.LogWarning("خروجی اکسل سفارش‌ها به سقف {Cap} ردیف رسید و ممکن است بخشی از سفارش‌ها در فایل نباشد.", MaxExcelExportRows);
            var userIds = orders.Where(o => !string.IsNullOrEmpty(o.UserId)).Select(o => o.UserId).Distinct().ToList();
            var users = new Dictionary<string, string>();
            if (userIds.Any())
            {
                users = await _userManager.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "نامشخص");
            }

            // ── خروجی اکسل واقعی (.xlsx) با ClosedXML ──
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("سفارشات");
            sheet.RightToLeft = true;

            string[] headers = { "کد سفارش", "نام کاربر", "گیرنده سفارش", "قیمت نهایی (تومان)", "وزن نهایی (گرم)", "وضعیت", "تاریخ" };
            for (int i = 0; i < headers.Length; i++)
                sheet.Cell(1, i + 1).Value = headers[i];

            var headerRange = sheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2;
            foreach (var order in orders)
            {
                string userName = (order.UserId != null && users.ContainsKey(order.UserId)) ? users[order.UserId] : "نامشخص";

                sheet.Cell(row, 1).Value = ExcelText(order.OrderCode);
                sheet.Cell(row, 2).Value = ExcelText(userName);
                sheet.Cell(row, 3).Value = ExcelText(order.CustomerName);

                if (order.FinalTotalAmount.HasValue)
                {
                    var priceCell = sheet.Cell(row, 4);
                    priceCell.Value = (double)order.FinalTotalAmount.Value;
                    priceCell.Style.NumberFormat.Format = "#,##0";
                }
                else
                {
                    sheet.Cell(row, 4).Value = "-";
                }

                if (order.FinalTotalWeightGrams.HasValue)
                {
                    var weightCell = sheet.Cell(row, 5);
                    weightCell.Value = (double)order.FinalTotalWeightGrams.Value;
                    weightCell.Style.NumberFormat.Format = "#,##0";
                }
                else
                {
                    sheet.Cell(row, 5).Value = "-";
                }

                sheet.Cell(row, 6).Value = ExcelText(order.OrderStatus.ToFarsi());
                sheet.Cell(row, 7).Value = ExcelText(new PersianDateTime(order.CreatedAt).ToString("yyyy/MM/dd"));
                row++;
            }

            int totalOrders = orders.Count;
            decimal totalPayments = orders.Sum(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot);

            row++; // یک ردیف خالی بین جدول و جمع‌بندی
            var totalOrdersCell = sheet.Cell(row, 1);
            totalOrdersCell.Value = "تعداد سفارشات";
            totalOrdersCell.Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = totalOrders;

            row++;
            var totalPaymentsCell = sheet.Cell(row, 1);
            totalPaymentsCell.Value = "جمع کل پرداخت‌ها (تومان)";
            totalPaymentsCell.Style.Font.Bold = true;
            var totalPaymentsValueCell = sheet.Cell(row, 2);
            totalPaymentsValueCell.Value = (double)totalPayments;
            totalPaymentsValueCell.Style.NumberFormat.Format = "#,##0";
            totalPaymentsValueCell.Style.Font.Bold = true;

            sheet.Columns(1, headers.Length).AdjustToContents();
            sheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Orders_{status}_{IranClock.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        /// <summary>
        /// حذف ردیف‌های تکراری BoxFinalInfo برای یک سفارش (فقط جدیدترین ردیف هر جعبه حفظ می‌شود).
        /// این تکراری‌ها در اثر دابل‌کلیک روی دکمه ثبت یا ثبت همزمان توسط دو کاربر ایجاد می‌شدند
        /// و باعث خطای ۵۰۰ در صفحه جزئیات سفارش مشتری می‌شدند.
        /// </summary>
        private async Task DedupeBoxFinalInfosAsync(int orderId)
        {
            var rows = await _salesDb.BoxFinalInfos
                .Where(b => b.OrderId == orderId)
                .OrderBy(b => b.Id)
                .ToListAsync();
            var keep = new HashSet<string>();
            var toRemove = new List<BoxFinalInfo>();
            foreach (var row in rows.AsEnumerable().Reverse()) // از جدیدترین به قدیمی‌ترین
            {
                if (!keep.Add(row.BoxTitle)) toRemove.Add(row);
            }
            if (toRemove.Count > 0)
            {
                _salesDb.BoxFinalInfos.RemoveRange(toRemove);
                await _salesDb.SaveChangesAsync();
            }
        }

        /// <summary>
        /// حذف ردیف‌های قیمت نهایی جعبه‌هایی که دیگر ردیف شیرینی در سفارش ندارند.
        /// این ردیف‌های یتیم (پس از حذف جعبه) باعث می‌شدند جمع سفارش با واقعیت نخواند.
        /// </summary>
        private async Task RemoveOrphanBoxFinalInfosAsync(Order order)
        {
            var liveBoxTitles = (order.Items ?? new List<OrderItem>())
                .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                .Select(i => i.BoxTitle!)
                .Distinct()
                .ToList();

            var boxInfos = await _salesDb.BoxFinalInfos.Where(b => b.OrderId == order.Id).ToListAsync();
            var orphans = boxInfos.Where(b => !liveBoxTitles.Contains(b.BoxTitle)).ToList();
            if (orphans.Count > 0)
                _salesDb.BoxFinalInfos.RemoveRange(orphans);
        }

        /// <summary>
        /// بازمحاسبه مبلغ/وزن سفارش پس از تغییر ردیف‌ها یا جعبه‌ها:
        /// پاک‌سازی ردیف‌های یتیم و نوشتن مبلغ نهایی از منبع واحد محاسبه مبلغ.
        /// </summary>
        private async Task RecalculateOrderTotalsAsync(int orderId)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return;

            await RemoveOrphanBoxFinalInfosAsync(order);
            await _pricingService.ApplyToOrderAsync(order);
            order.UpdatedAt = DateTime.UtcNow;
            await _salesDb.SaveChangesAsync();
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBoxWeight(int orderId, string boxTitle, int finalWeightGrams, decimal finalPrice, string? adminNotes)
        {
            await DedupeBoxFinalInfosAsync(orderId);

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

            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order != null)
            {
                // مبلغ/وزن نهایی از تنها منبع محاسبه مبلغ برنامه محاسبه میشود
                // (شامل تخفیف، هزینه پیک، ارسال رایگان و فقط جعبه‌های وزن‌کشی‌شده)
                await _pricingService.ApplyToOrderAsync(order);
                order.IsPaymentEnabled = true;
                order.OrderStatus = OrderStatus.PendingPayment;
                order.PaymentStatus = PaymentStatus.Unpaid;
                order.UpdatedAt = DateTime.UtcNow;
                await _salesDb.SaveChangesAsync();

                // ── پیامک «سفارش شما بررسی و آماده پرداخت است» ──
                try { await _sms.NotifyWeighingReadyAsync(order); }
                catch (Exception smsEx) { _logger.LogWarning(smsEx, "SMS weighing notify failed for order {OrderId}", orderId); }
            }

            // اگر فاکتوری صادر شده، با قیمت/وزن نهایی جدید جعبه همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, orderId);

            TempData["SuccessMessage"] = $"وزن و قیمت نهایی جعبه {boxTitle} ثبت شد. مشتری می‌تواند مبلغ قابل پرداخت را تسویه کند.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        /// <summary>
        /// ثبت یکجا وزن و قیمت همه جعبه‌های یک سفارش (یک دکمه برای کل صفحه):
        /// آرایه‌های boxTitle/finalWeightGrams/finalPrice با ایندکس هم‌تراز ارسال می‌شوند.
        /// </summary>
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizeOrderWeights(int orderId, string[] boxTitle, int[] finalWeightGrams, decimal[] finalPrice, string[]? adminNotes, int[]? itemWeightId = null, int[]? itemWeight = null)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();
            if (boxTitle == null || boxTitle.Length == 0)
            {
                TempData["ErrorMessage"] = "هیچ جعبه‌ای برای ثبت وجود ندارد.";
                return RedirectToAction("OrderDetails", new { id = orderId });
            }

            // ۱) وزن تک‌تک ردیف‌های شیرینی جعبه‌ها ثبت می‌شود (وزن‌کشی واقعی توسط ادمین)
            await ApplyRowWeightsAsync(order, itemWeightId, itemWeight);

            // ⛔ کنترل ظرفیت: اگر جمع وزن ردیف‌های یک جعبه از ظرفیت نوع آن جعبه بیشتر باشد،
            // هیچ تغییری ذخیره نمی‌شود (فقط در حافظه اعمال شده و ذخیره‌سازی انجام نمی‌گیرد).
            var capacityError = await FindBoxCapacityViolationAsync(order, boxTitle, finalWeightGrams);
            if (capacityError != null)
            {
                TempData["ErrorMessage"] = capacityError;
                return RedirectToAction("OrderDetails", new { id = orderId });
            }

            // ۲) وزن/قیمت نهایی جعبه‌هایی که ادمین دستی وارد نکرده، از روی وزن ردیف‌ها محاسبه می‌شود
            var computedTotals = await ComputeBoxTotalsFromRowsAsync(order);

            // پاک‌سازی ردیف‌های تکراریِ قبلی تا دوباره تکراری ساخته نشود
            await DedupeBoxFinalInfosAsync(orderId);

            for (int i = 0; i < boxTitle.Length; i++)
            {
                var title = boxTitle[i];
                if (string.IsNullOrWhiteSpace(title)) continue;
                var weight = i < finalWeightGrams.Length ? finalWeightGrams[i] : 0;
                var price = i < finalPrice.Length ? finalPrice[i] : 0m;
                var note = adminNotes != null && i < adminNotes.Length ? adminNotes[i] : null;

                // مقدار دستی ادمین مقدم است؛ فقط اگر خالی/صفر باشد از جمع وزن ردیف‌ها محاسبه می‌شود
                if (computedTotals.TryGetValue(title, out var calculated))
                {
                    if (weight <= 0) weight = calculated.Weight;
                    if (price <= 0) price = calculated.Price;
                }

                var existing = await _salesDb.BoxFinalInfos
                    .FirstOrDefaultAsync(b => b.OrderId == orderId && b.BoxTitle == title);
                if (existing != null)
                {
                    existing.FinalWeightGrams = weight;
                    existing.FinalPrice = price;
                    existing.AdminNotes = note;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _salesDb.BoxFinalInfos.Add(new BoxFinalInfo
                    {
                        OrderId = orderId,
                        BoxTitle = title,
                        FinalWeightGrams = weight,
                        FinalPrice = price,
                        AdminNotes = note,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _salesDb.SaveChangesAsync();

            // مبلغ/وزن نهایی از تنها منبع محاسبه مبلغ برنامه محاسبه میشود
            await _pricingService.ApplyToOrderAsync(order);
            order.IsPaymentEnabled = true;
            order.OrderStatus = OrderStatus.PendingPayment;
            order.PaymentStatus = PaymentStatus.Unpaid;
            order.UpdatedAt = DateTime.UtcNow;
            await _salesDb.SaveChangesAsync();

            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, orderId);

            try { await _sms.NotifyWeighingReadyAsync(order); }
            catch (Exception smsEx) { _logger.LogWarning(smsEx, "SMS weighing notify failed for order {OrderId}", orderId); }

            TempData["SuccessMessage"] = $"وزن و قیمت {boxTitle.Length} جعبه با هم ثبت شد. مشتری می‌تواند مبلغ قابل پرداخت را تسویه کند.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        /// <summary>
        /// ظرفیت نوع جعبه‌ی هر جعبه سفارش (کلید = نام جعبه).
        /// نوع جعبه از ردیف‌های همان جعبه خوانده می‌شود (OrderItem.BoxTypeId که هنگام پرکردن جعبه ثبت شده).
        /// </summary>
        private async Task<Dictionary<string, BoxCapacityViewModel>> GetBoxCapacitiesAsync(Order order)
        {
            var result = new Dictionary<string, BoxCapacityViewModel>();

            var rows = (order.Items ?? new List<OrderItem>())
                .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                .ToList();
            if (rows.Count == 0) return result;

            var typeIds = rows.Where(r => r.BoxTypeId.HasValue).Select(r => r.BoxTypeId!.Value).Distinct().ToList();
            if (typeIds.Count == 0) return result;

            var types = await _catalogDb.BoxTypes
                .AsNoTracking()
                .Where(b => typeIds.Contains(b.Id))
                .ToListAsync();

            foreach (var group in rows.GroupBy(r => r.BoxTitle!))
            {
                var typeId = group.Select(r => r.BoxTypeId).FirstOrDefault(id => id.HasValue);
                var type = typeId.HasValue ? types.FirstOrDefault(t => t.Id == typeId.Value) : null;
                if (type == null) continue;

                result[group.Key] = new BoxCapacityViewModel
                {
                    BoxTypeId = type.Id,
                    TypeTitle = type.TitleFa,
                    CapacityGrams = type.CapacityGrams,
                    MaxRows = type.MaxRows
                };
            }

            return result;
        }

        /// <summary>
        /// کنترل ظرفیت جعبه‌ها پیش از ثبت وزن.
        /// اگر جمع وزن ردیف‌های یک جعبه (با وزن‌های ارسالی همین درخواست) یا وزن نهایی واردشده ادمین،
        /// از ظرفیت نوع همان جعبه بیشتر باشد، پیام خطای فارسی برگردانده می‌شود و هیچ چیزی ذخیره نمی‌شود.
        /// </summary>
        private async Task<string?> FindBoxCapacityViolationAsync(Order order, string[]? boxTitles, int[]? submittedFinalWeights)
        {
            var capacities = await GetBoxCapacitiesAsync(order);
            if (capacities.Count == 0) return null; // جعبه‌های قدیمی که نوع جعبه‌شان ثبت نشده، کنترل نمی‌شوند

            // وزن نهایی ارسالی فرم (کلید = نام جعبه)
            var submitted = new Dictionary<string, int>();
            if (boxTitles != null && submittedFinalWeights != null)
            {
                var count = Math.Min(boxTitles.Length, submittedFinalWeights.Length);
                for (int i = 0; i < count; i++)
                {
                    if (string.IsNullOrWhiteSpace(boxTitles[i])) continue;
                    submitted[boxTitles[i]] = submittedFinalWeights[i];
                }
            }

            var violations = new List<string>();
            foreach (var group in (order.Items ?? new List<OrderItem>())
                .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                .GroupBy(i => i.BoxTitle!))
            {
                if (!capacities.TryGetValue(group.Key, out var capacity)) continue;

                // فقط ردیف‌هایی که وزن‌کشی شده‌اند شمرده می‌شوند (وزن صفر = هنوز وزن نشده)
                var rowsWeight = group.Where(r => (r.WeightSnapshotGrams ?? 0) > 0).Sum(r => r.WeightSnapshotGrams!.Value);
                var finalWeight = submitted.TryGetValue(group.Key, out var fw) ? fw : 0;
                var effectiveWeight = Math.Max(rowsWeight, finalWeight);

                if (effectiveWeight > capacity.CapacityGrams)
                {
                    violations.Add($"«{group.Key}» ({capacity.TypeTitle}): وزن {effectiveWeight.ToString("N0").ToPersianNumber()} گرم در برابر ظرفیت {capacity.CapacityGrams.ToString("N0").ToPersianNumber()} گرم");
                }
            }

            if (violations.Count == 0) return null;

            return "⛔ ثبت وزن انجام نشد؛ وزن از ظرفیت جعبه بیشتر است. " + string.Join(" | ", violations) +
                   ". برای ادامه، وزن ردیف‌ها را اصلاح کنید یا با نوع جعبه بزرگ‌تر (یا جعبه بیشتر) سفارش را وزن‌کشی کنید.";
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        /// <summary>
        /// ثبت وزن تک‌تک ردیف‌های شیرینی جعبه‌ها (ورودی ادمین پس از وزن‌کشی) و به‌روزرسانی قیمت همان ردیف.
        /// مبنای قیمت: قیمت هر کیلوگرم همان شیرینی (تعریف‌شده در /SweetItems/Create) × وزن ردیف.
        /// </summary>
        private async Task ApplyRowWeightsAsync(Order order, int[]? itemWeightId, int[]? itemWeight)
        {
            if (itemWeightId == null || itemWeight == null || itemWeightId.Length == 0) return;

            var rows = (order.Items ?? new List<OrderItem>())
                .Where(i => i.ItemType == OrderItemType.SweetItem && i.SweetItemId.HasValue)
                .GroupBy(i => i.Id)
                .ToDictionary(g => g.Key, g => g.First());
            if (rows.Count == 0) return;

            var sweetIds = rows.Values.Select(i => i.SweetItemId!.Value).Distinct().ToList();
            var prices = await _catalogDb.SweetItems
                .Where(s => sweetIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.PricePerKg);

            var count = Math.Min(itemWeightId.Length, itemWeight.Length);
            for (int i = 0; i < count; i++)
            {
                // ردیف‌هایی که به این سفارش تعلق ندارند نادیده گرفته می‌شوند
                if (!rows.TryGetValue(itemWeightId[i], out var row)) continue;

                var weight = itemWeight[i];
                if (weight < 0) weight = 0;
                if (weight > 20000) weight = 20000; // سقف ایمنی ۲۰ کیلوگرم برای هر ردیف

                var pricePerKg = prices.TryGetValue(row.SweetItemId!.Value, out var p) ? p : 0m;
                var quantity = row.Quantity < 1 ? 1 : row.Quantity;

                row.WeightSnapshotGrams = weight;
                row.UnitPriceSnapshot = (weight / 1000m) * pricePerKg;
                row.TotalPriceSnapshot = row.UnitPriceSnapshot * quantity;
            }
        }

        /// <summary>
        /// محاسبه خودکار وزن و قیمت هر جعبه از روی وزن ردیف‌های همان جعبه.
        /// جمع وزن ردیف‌ها = وزن نهایی جعبه و جمع قیمت ردیف‌ها = قیمت نهایی جعبه.
        /// </summary>
        private async Task<Dictionary<string, (int Weight, decimal Price)>> ComputeBoxTotalsFromRowsAsync(Order order)
        {
            var rows = (order.Items ?? new List<OrderItem>())
                .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                .ToList();

            var sweetIds = rows.Where(i => i.SweetItemId.HasValue).Select(i => i.SweetItemId!.Value).Distinct().ToList();
            var prices = sweetIds.Count > 0
                ? await _catalogDb.SweetItems.Where(s => sweetIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.PricePerKg)
                : new Dictionary<int, decimal>();

            return rows
                .GroupBy(i => i.BoxTitle!)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var weight = g.Sum(i => i.WeightSnapshotGrams ?? 0);
                        var price = 0m;
                        foreach (var row in g)
                        {
                            var pricePerKg = row.SweetItemId.HasValue && prices.TryGetValue(row.SweetItemId.Value, out var p) ? p : 0m;
                            var quantity = row.Quantity < 1 ? 1 : row.Quantity;
                            price += ((row.WeightSnapshotGrams ?? 0) / 1000m) * pricePerKg * quantity;
                        }
                        return (weight, price);
                    });
        }

        /// <summary>
        /// ثبت وزن تک‌تک ردیف‌های جعبه‌ها و سپس محاسبه خودکار وزن/قیمت نهایی هر جعبه.
        /// برخلاف FinalizeOrderWeights وضعیت سفارش را تغییر نمی‌دهد و پرداخت را فعال نمی‌کند؛
        /// برای فعال کردن درگاه پرداخت مشتری همان دکمه انتهای صفحه استفاده می‌شود.
        /// </summary>
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBoxRowWeights(int orderId, int[]? itemWeightId, int[]? itemWeight, string[]? boxTitle, int[]? finalWeightGrams, decimal[]? finalPrice, string[]? adminNotes)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            await ApplyRowWeightsAsync(order, itemWeightId, itemWeight);

            // ⛔ کنترل ظرفیت: وزن بیشتر از ظرفیت نوع جعبه ذخیره نمی‌شود
            var capacityError = await FindBoxCapacityViolationAsync(order, boxTitle, finalWeightGrams);
            if (capacityError != null)
            {
                TempData["ErrorMessage"] = capacityError;
                return RedirectToAction("OrderDetails", new { id = orderId });
            }

            var computedTotals = await ComputeBoxTotalsFromRowsAsync(order);
            await UpsertBoxFinalInfosAsync(order, boxTitle, finalWeightGrams, finalPrice, adminNotes, computedTotals);

            // ابتدا ذخیره می‌شود: محاسبه مبلغ، ردیف‌های BoxFinalInfos را از دیتابیس و بدون ردیابی می‌خواند
            await _salesDb.SaveChangesAsync();

            // مبلغ سفارش از تنها منبع محاسبه مبلغ بازمحاسبه می‌شود (مبلغی که دریافت می‌شود)
            await _pricingService.ApplyToOrderAsync(order);
            order.UpdatedAt = DateTime.UtcNow;
            await _salesDb.SaveChangesAsync();

            // اگر فاکتوری صادر شده، با وزن/قیمت جدید هم‌گام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, orderId);

            TempData["SuccessMessage"] = "وزن ردیف‌ها ثبت و وزن/قیمت نهایی جعبه‌ها محاسبه شد.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        /// <summary>
        /// درج/به‌روزرسانی وزن و قیمت نهایی جعبه‌ها.
        /// مقدار دستی ادمین مقدم است؛ اگر وزن/قیمت ارسالی صفر باشد، مقدار محاسبه‌شده از وزن ردیف‌ها نوشته می‌شود.
        /// </summary>
        private async Task UpsertBoxFinalInfosAsync(
            Order order,
            string[]? boxTitles,
            int[]? finalWeights,
            decimal[]? finalPrices,
            string[]? adminNotes,
            Dictionary<string, (int Weight, decimal Price)> computedTotals)
        {
            if (boxTitles == null || boxTitles.Length == 0) return;

            await DedupeBoxFinalInfosAsync(order.Id);

            var existingRows = await _salesDb.BoxFinalInfos.Where(b => b.OrderId == order.Id).ToListAsync();

            for (int i = 0; i < boxTitles.Length; i++)
            {
                var title = boxTitles[i];
                if (string.IsNullOrWhiteSpace(title)) continue;

                var weight = finalWeights != null && i < finalWeights.Length ? finalWeights[i] : 0;
                var price = finalPrices != null && i < finalPrices.Length ? finalPrices[i] : 0m;
                var note = adminNotes != null && i < adminNotes.Length ? adminNotes[i] : null;

                computedTotals.TryGetValue(title, out var calculated);
                if (weight <= 0) weight = calculated.Weight;
                if (price <= 0) price = calculated.Price;

                var existing = existingRows.FirstOrDefault(b => b.BoxTitle == title);

                // جعبه‌ای که هنوز هیچ وزنی برایش ثبت نشده، ردیف قیمت هم نمی‌سازد
                if (existing == null && weight <= 0 && price <= 0) continue;

                if (existing != null)
                {
                    existing.FinalWeightGrams = weight;
                    existing.FinalPrice = price;
                    if (note != null) existing.AdminNotes = note;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _salesDb.BoxFinalInfos.Add(new BoxFinalInfo
                    {
                        OrderId = order.Id,
                        BoxTitle = title,
                        FinalWeightGrams = weight,
                        FinalPrice = price,
                        AdminNotes = note,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDeliveryFee(int orderId, decimal deliveryFee)
        {
            // ردیف‌های سفارش باید بارگذاری شوند؛ وگرنه محاسبه مبلغ روی مجموعه خالی انجام میشد
            // و (باگ قبلی) قیمت جعبه‌ها با عدد صفر بازنویسی می‌شد.
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            order.DeliveryFeeSnapshot = deliveryFee;

            // مبلغ نهایی همیشه از منبع واحد محاسبه می‌شود تا با مبلغی که از درگاه دریافت می‌شود یکی باشد
            await _pricingService.ApplyToOrderAsync(order);

            // فقط سفارش‌های در جریان با ثبت هزینه پیک قابل پرداخت میشوند؛
            // سفارش پرداخت‌شده نباید دوباره «پرداخت‌نشده» شود.
            if (order.OrderStatus == OrderStatus.AwaitingReview || order.OrderStatus == OrderStatus.PendingPayment)
            {
                order.IsPaymentEnabled = true;
                order.OrderStatus = OrderStatus.PendingPayment;
                order.PaymentStatus = PaymentStatus.Unpaid;
            }

            order.UpdatedAt = DateTime.UtcNow;
            await _salesDb.SaveChangesAsync();

            // اگر فاکتوری صادر شده، با هزینه پیک جدید همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, orderId);

            TempData["SuccessMessage"] = $"هزینه پیک {deliveryFee.ToString("N0")} تومان ثبت شد.";
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

            // در صورت وجود ردیف تکراری برای یک جعبه (دابل‌کلیک ثبت)، فقط جدیدترین ردیف نمایش داده می‌شود
            var boxInfos = (await _salesDb.BoxFinalInfos
                    .Where(b => b.OrderId == id)
                    .ToListAsync())
                .GroupBy(b => b.BoxTitle)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id).First());

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

            // قیمت هر کیلو و وزن تقریبی هر شیرینی: مبنای محاسبه خودکار وزن/قیمت ردیف‌های جعبه
            var sweetPricePerKg = new Dictionary<int, decimal>();
            var sweetApproxWeight = new Dictionary<int, int>();
            if (sweetItemIds.Any())
            {
                var sweetRows = await _catalogDb.SweetItems
                    .Where(x => sweetItemIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.PricePerKg, x.ApproxWeightGrams })
                    .ToListAsync();
                sweetPricePerKg = sweetRows.ToDictionary(x => x.Id, x => x.PricePerKg);
                sweetApproxWeight = sweetRows.ToDictionary(x => x.Id, x => x.ApproxWeightGrams);
            }

            ViewBag.SweetNames = sweetNames;
            ViewBag.SweetPricePerKg = sweetPricePerKg;
            ViewBag.SweetApproxWeight = sweetApproxWeight;
            // ظرفیت هر جعبه: مبنای هشدار زنده و غیرفعال‌کردن دکمه ثبت وقتی وزن از ظرفیت جعبه بیشتر شود
            ViewBag.BoxCapacities = await GetBoxCapacitiesAsync(order);
            ViewBag.ProductNames = productNames;
            ViewBag.SweetListJson = JsonSerializer.Serialize(sweetListForJs);
            ViewBag.BoxTypeTitle = order.Items.FirstOrDefault()?.BoxTitle ?? "نوع جعبه مشخص نیست";

            return View(order);
        }

        /// <summary>
        /// برچسب چاپی وزن‌کشی جعبه‌ها: ریز وزن هر ردیف شیرینی و وزن/قیمت نهایی هر جعبه،
        /// برای چاپ و چسباندن روی همان جعبه پیش از تحویل.
        /// اعداد از همان مبنایی می‌آید که پنل و فاکتور استفاده می‌کنند
        /// (وزن ردیف‌ها + وزن/قیمت نهایی ثبت‌شده در BoxFinalInfos).
        /// </summary>
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> BoxLabels(int orderId, string? boxTitle = null)
        {
            var order = await _salesDb.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            var boxRows = (order.Items ?? new List<OrderItem>())
                .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                .ToList();

            var boxTitles = boxRows.Select(i => i.BoxTitle!).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (!string.IsNullOrWhiteSpace(boxTitle))
                boxTitles = boxTitles.Where(t => string.Equals(t, boxTitle.Trim(), StringComparison.Ordinal)).ToList();

            if (boxTitles.Count == 0)
            {
                TempData["ErrorMessage"] = "برای این سفارش جعبه‌ای با این نام وجود ندارد؛ برچسبی برای چاپ ساخته نشد.";
                return RedirectToAction("OrderDetails", new { id = orderId });
            }

            // جدیدترین وزن/قیمت ثبت‌شده هر جعبه = مبنای قیمت‌گذاری (ردیف تکراری نادیده گرفته می‌شود)
            var boxInfos = (await _salesDb.BoxFinalInfos
                    .AsNoTracking()
                    .Where(b => b.OrderId == orderId)
                    .ToListAsync())
                .GroupBy(b => b.BoxTitle)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id).First());

            var sweetIds = boxRows.Where(i => i.SweetItemId.HasValue).Select(i => i.SweetItemId!.Value).Distinct().ToList();
            var sweetById = (await _catalogDb.SweetItems
                    .AsNoTracking()
                    .Where(s => sweetIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.TitleFa, s.PricePerKg, s.ApproxWeightGrams })
                    .ToListAsync())
                .ToDictionary(s => s.Id);

            var store = await _salesDb.SiteSettings.AsNoTracking().FirstOrDefaultAsync();

            var vm = new BoxLabelViewModel
            {
                StoreName = string.IsNullOrWhiteSpace(store?.SiteTitle) ? "شیرینی سرای تک" : store!.SiteTitle!,
                StorePhone = store?.Phone ?? "",
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                OrderDate = new PersianDateTime(order.CreatedAt).ToString("yyyy/MM/dd"),
                PrintDate = new PersianDateTime(IranClock.Now).ToString("yyyy/MM/dd HH:mm")
            };

            int boxNumber = 0;
            foreach (var title in boxTitles)
            {
                var labelRows = new List<BoxLabelRow>();
                var rowsWeight = 0;
                var rowsPrice = 0m;

                foreach (var row in boxRows.Where(i => i.BoxTitle == title).OrderBy(i => i.Id))
                {
                    sweetById.TryGetValue(row.SweetItemId ?? 0, out var sweet);
                    var pricePerKg = sweet?.PricePerKg ?? 0m;
                    var quantity = row.Quantity < 1 ? 1 : row.Quantity;
                    var weighed = row.WeightSnapshotGrams ?? 0;
                    // ردیفی که هنوز وزن‌کشی نشده، وزن تقریبی همان شیرینی را نشان می‌دهد و برچسب «تقریبی» می‌گیرد
                    var weight = weighed > 0 ? weighed : (sweet?.ApproxWeightGrams ?? 0);
                    var rowPrice = (weight / 1000m) * pricePerKg * quantity;

                    rowsWeight += weight;
                    rowsPrice += rowPrice;
                    labelRows.Add(new BoxLabelRow
                    {
                        Name = sweet?.TitleFa ?? $"شیرینی (شناسه {row.SweetItemId})",
                        Quantity = quantity,
                        WeightGrams = weight,
                        IsApproximate = weighed <= 0,
                        PricePerKg = pricePerKg,
                        RowPrice = rowPrice
                    });
                }

                boxInfos.TryGetValue(title, out var info);
                var finalWeight = info?.FinalWeightGrams ?? rowsWeight;
                boxNumber++;

                vm.Boxes.Add(new BoxLabelBox
                {
                    BoxTitle = title,
                    BoxNumber = boxNumber,
                    BoxCount = boxTitles.Count,
                    Rows = labelRows,
                    RowsWeightGrams = rowsWeight,
                    RowsPrice = rowsPrice,
                    FinalWeightGrams = finalWeight,
                    FinalPrice = info?.FinalPrice ?? rowsPrice,
                    IsFinalized = info != null,
                    Notes = info?.AdminNotes,
                    QrPayload = $"{order.OrderCode}|{title}|{finalWeight}g"
                });
            }

            return View("BoxLabels", vm);
        }

        /// <summary>
        /// QR برچسب جعبه (SVG). متن داخل کد از خود سرور ساخته می‌شود
        /// (کد سفارش | نام جعبه | وزن نهایی) تا هنگام بسته‌بندی با موبایل قابل اسکن باشد.
        /// </summary>
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> BoxLabelQr(int orderId, string title = "", int weight = 0)
        {
            var order = await _salesDb.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => new { o.OrderCode })
                .FirstOrDefaultAsync();
            if (order == null) return NotFound();

            var safeTitle = title.Length > 100 ? title[..100] : title;
            if (weight < 0) weight = 0;
            var payload = $"{order.OrderCode}|{safeTitle}|{weight}g";

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var svg = new SvgQRCode(data).GetGraphic(3, "#000000", "#FFFFFF");
            Response.Headers["Cache-Control"] = "private, max-age=300";
            return Content(svg, "image/svg+xml");
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrderItem(int itemId)
        {
            var item = await _salesDb.OrderItems.FindAsync(itemId);
            if (item == null) return NotFound();

            _salesDb.OrderItems.Remove(item);
            await _salesDb.SaveChangesAsync();

            // مبلغ سفارش باید پس از حذف ردیف بازمحاسبه شود، وگرنه عدد نمایش‌داده‌شده با مبلغ دریافتی فرق می‌کند
            await RecalculateOrderTotalsAsync(item.OrderId);

            // اگر فاکتوری صادر شده، با وضعیت جدید سفارش همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, item.OrderId);

            return Ok(new { success = true, message = "آیتم با موفقیت حذف شد." });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
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

            // مبلغ سفارش با تغییر ردیف بازمحاسبه می‌شود
            await RecalculateOrderTotalsAsync(item.OrderId);

            // اگر فاکتوری صادر شده، با وضعیت جدید سفارش همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, item.OrderId);

            return Ok(new { success = true, message = "آیتم با موفقیت به‌روزرسانی شد." });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderWeight(int orderId, int finalWeightGrams, decimal finalPrice, string? adminNotes)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            // ⛔ کنترل ظرفیت جعبه‌ها در همین مسیر منسوخ هم انجام می‌شود تا راهی برای دورزدن
            // کنترل وزن جعبه‌ها و فعال‌کردن پرداخت باقی نماند.
            var capacityError = await FindBoxCapacityViolationAsync(order, null, null);
            if (capacityError != null)
            {
                TempData["ErrorMessage"] = capacityError;
                return RedirectToAction("OrderDetails", new { id = orderId });
            }

            // ⚠️ ثبت «قیمت نهایی دستی» منسوخ شده است: مبلغی که از مشتری گرفته میشود از منبع واحد
            // محاسبه مبلغ میآید (محصولات + وزنهای تأییدشده + پیک − تخفیف)، پس قیمت دستی در
            // محاسبه لحاظ نمیشد و فقط نمایش را از مبلغ دریافتی جدا میکرد.
            // اکنون فقط وزن و یادداشت ذخیره میشود و مبلغ از داده واقعی سفارش بازمحاسبه میگردد؛
            // برای تعیین قیمت، از «ثبت وزن جعبهها» در صفحه جزئیات سفارش استفاده کنید.
            if (finalPrice > 0)
                _logger.LogWarning("UpdateOrderWeight received a manual price ({FinalPrice}) for order {OrderId}; the manual price is ignored and the order amount is recomputed from box weights.", finalPrice, orderId);

            var hadWeight = finalWeightGrams > 0;
            if (hadWeight)
                order.FinalTotalWeightGrams = finalWeightGrams;
            order.AdminNotes = adminNotes;
            await _pricingService.ApplyToOrderAsync(order);

            order.IsPaymentEnabled = true;
            order.OrderStatus = OrderStatus.PendingPayment;
            order.PaymentStatus = PaymentStatus.Unpaid;
            order.UpdatedAt = DateTime.UtcNow;

            await _salesDb.SaveChangesAsync();

            // اگر فاکتوری صادر شده، با وضعیت جدید سفارش همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, orderId);

            // ── پیامک «سفارش شما بررسی و آماده پرداخت است» پس از وزن‌کشی ──
            try { await _sms.NotifyWeighingReadyAsync(order); }
            catch (Exception smsEx) { _logger.LogWarning(smsEx, "SMS weighing notify failed for order {OrderId}", orderId); }

            TempData["SuccessMessage"] = "وزن و قیمت نهایی ثبت شد. کاربر می‌تواند سفارش را پرداخت کند.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus status)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            var oldStatus = order.OrderStatus;
            order.OrderStatus = status;
            order.UpdatedAt = DateTime.UtcNow;

            await _salesDb.SaveChangesAsync();

            // ── پیامک‌های خودکار بر اساس وضعیت جدید ──
            try
            {
                if (status == OrderStatus.Preparing && oldStatus != OrderStatus.Preparing)
                {
                    await _sms.NotifyOrderStatusChangedAsync(order, "در حال آماده‌سازی");
                }
                else if (status == OrderStatus.Shipped && oldStatus != OrderStatus.Shipped)
                {
                    await _sms.NotifyOrderStatusChangedAsync(order, "ارسال شد");
                }
                else if (status == OrderStatus.Delivered && oldStatus != OrderStatus.Delivered)
                {
                    await _sms.NotifyOrderStatusChangedAsync(order, "تحویل شد");
                }
            }
            catch (Exception smsEx)
            {
                _logger.LogWarning(smsEx, "SMS status-change notify failed for order {OrderId}", orderId);
            }

            // Note: inventory is now deducted only in PaymentController.Callback after successful payment
            if (status == OrderStatus.Delivered && oldStatus != OrderStatus.Delivered && order.PaymentStatus == PaymentStatus.Succeeded)
                await ApplyCashbackToWallet(order);

            TempData["SuccessMessage"] = $"وضعیت سفارش به {status.ToFarsi()} تغییر کرد.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        private async Task ApplyCashbackToWallet(Order order)
        {
            var settings = await _salesDb.WalletSettings.FirstOrDefaultAsync();
            if (settings == null || !settings.IsEnabled) return;

            // جلوگیری از کشبک تکراری برای یک سفارش (مثلاً تغییر وضعیت چندباره به تحویل‌شده)
            var cashbackAlreadyPaid = await _salesDb.WalletTransactions
                .AnyAsync(t => t.OrderId == order.Id && t.Type == "Cashback");
            if (cashbackAlreadyPaid) return;

            // مبنای کش‌بک، مبلغی است که واقعاً دریافت شده (درگاه + کیف پول)، نه برآورد لحظه ثبت سفارش
            var pricing = await _pricingService.ComputeAsync(order);
            var cashbackBase = pricing.PaidTotal > 0 ? pricing.PaidTotal : order.TotalAmountSnapshot;

            decimal cashbackAmount = 0;
            if (settings.ReturnType == "Percentage")
                cashbackAmount = (cashbackBase * settings.ReturnValue) / 100;
            else if (settings.ReturnType == "Fixed")
                cashbackAmount = settings.ReturnValue;

            if (cashbackAmount <= 0) return;

            if (cashbackBase >= settings.MinimumOrderAmount)
            {
                var wallet = await _salesDb.Wallets.FirstOrDefaultAsync(w => w.UserId == order.UserId);
                if (wallet == null)
                {
                    wallet = new Wallet { UserId = order.UserId!, Balance = 0 };
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
                        UserId = order.UserId!,
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBox(int orderId, string boxTitle)
        {
            var items = await _salesDb.OrderItems.Where(x => x.OrderId == orderId && x.BoxTitle == boxTitle).ToListAsync();
            if (!items.Any()) return NotFound();

            _salesDb.OrderItems.RemoveRange(items);
            await _salesDb.SaveChangesAsync();

            // ردیف قیمت نهایی همان جعبه هم حذف و مبلغ سفارش بازمحاسبه می‌شود
            var boxInfos = await _salesDb.BoxFinalInfos.Where(b => b.OrderId == orderId && b.BoxTitle == boxTitle).ToListAsync();
            if (boxInfos.Count > 0)
            {
                _salesDb.BoxFinalInfos.RemoveRange(boxInfos);
                await _salesDb.SaveChangesAsync();
            }
            await RecalculateOrderTotalsAsync(orderId);

            // اگر فاکتوری صادر شده، با وضعیت جدید سفارش همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, orderId);

            TempData["SuccessMessage"] = $"جعبه {boxTitle} با موفقیت حذف شد.";
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrder(int orderId)
        {
            var order = await _salesDb.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            // بازگشت وجه کیف پول فقط برای سفارش‌های پرداخت‌نشده (idempotent؛ یک‌بار انجام می‌شود)
            if (order.PaymentStatus != PaymentStatus.Succeeded)
                await OrderWalletHelper.RefundWalletAsync(_salesDb, order);

            var boxInfos = await _salesDb.BoxFinalInfos.Where(b => b.OrderId == orderId).ToListAsync();
            if (boxInfos.Count > 0)
                _salesDb.BoxFinalInfos.RemoveRange(boxInfos);

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
        [ValidateAntiForgeryToken]
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

            // مبلغ سفارش با تغییر ردیف بازمحاسبه می‌شود
            await RecalculateOrderTotalsAsync(item.OrderId);

            // اگر فاکتوری صادر شده، با وضعیت جدید سفارش همگام می‌شود
            await InvoiceService.RegenerateIfExistsAsync(_salesDb, _catalogDb, item.OrderId);

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
        public async Task<IActionResult> Settings()
        {
            var site = await _salesDb.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var wallet = await _salesDb.WalletSettings.AsNoTracking().FirstOrDefaultAsync();

            ViewBag.FreeDeliveryThreshold = site?.FreeDeliveryThreshold ?? 0m;
            ViewBag.WalletCashbackMode = wallet?.ReturnType ?? "Percentage";
            ViewBag.WalletCashbackPercent = wallet != null && wallet.ReturnType == "Percentage" ? wallet.ReturnValue : 0m;

            return View();
        }

        /// <summary>
        /// تنظیمات فروشگاه: آستانه ارسال رایگان و درصد بازگشت وجه کیف پول.
        /// هر دو مقدار واقعاً ذخیره و استفاده میشوند:
        /// آستانه ارسال رایگان در محاسبه هزینه پیک (OrderPricingService) و
        /// درصد بازگشت وجه در اعمال کش‌بک هنگام تحویل سفارش.
        /// </summary>
        [Authorize(Roles = "Admin,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(decimal freeDeliveryThreshold, decimal walletCashbackPercent)
        {
            if (freeDeliveryThreshold < 0 || walletCashbackPercent < 0 || walletCashbackPercent > 100)
            {
                TempData["ErrorMessage"] = "مقادیر وارد‌شده معتبر نیستند (آستانه نمیتواند منفی و درصد بازگشت وجه باید بین ۰ تا ۱۰۰ باشد).";
                return RedirectToAction("Settings");
            }

            var site = await _salesDb.SiteSettings.FirstOrDefaultAsync();
            if (site == null)
            {
                site = new SiteSetting();
                _salesDb.SiteSettings.Add(site);
            }
            site.FreeDeliveryThreshold = freeDeliveryThreshold;
            site.UpdatedAt = DateTime.UtcNow;

            var wallet = await _salesDb.WalletSettings.FirstOrDefaultAsync();
            if (wallet == null)
            {
                wallet = new WalletSettings();
                _salesDb.WalletSettings.Add(wallet);
            }
            wallet.ReturnType = "Percentage";
            wallet.ReturnValue = walletCashbackPercent;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _salesDb.SaveChangesAsync();

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
        public async Task<IActionResult> UpdateCustomCakeOrder(int id, CustomCakeOrderStatus status, decimal? finalPrice, decimal? deliveryFee, string? adminNotes)
        {
            var order = await _salesDb.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = status;
            order.FinalPrice = finalPrice;
            // هزینه پیک فقط برای سفارش‌های «ارسال با پیک» ثبت می‌شود
            order.DeliveryFee = order.DeliveryMethod == DeliveryMethod.Delivery ? deliveryFee : null;
            order.AdminNotes = adminNotes;
            order.UpdatedAt = DateTime.UtcNow;

            await _salesDb.SaveChangesAsync();

            // ── پیامک تغییر وضعیت کیک سفارشی به مشتری ──
            try { await _sms.NotifyCustomCakeStatusAsync(order, status.ToString()); }
            catch (Exception smsEx) { _logger.LogWarning(smsEx, "SMS custom-cake notify failed for cake order {CakeOrderId}", id); }

            TempData["SuccessMessage"] = "وضعیت سفارش کیک به‌روزرسانی شد.";
            return RedirectToAction("CustomCakeOrders");
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> EditCustomCakeOrder(int id)
        {
            var order = await _salesDb.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(order.UserId))
            {
                ViewData["CakeDeliveryAddresses"] = await _salesDb.Addresses
                    .Where(a => a.UserId == order.UserId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.CreatedAt)
                    .ToListAsync();
            }
            ViewData["CakeDeliveryMethod"] = (int)order.DeliveryMethod;
            ViewData["CakeDeliveryAddressId"] = order.AddressId;
            return View(order);
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomCakeOrder(int id, CustomCakeOrder model,
            string? desiredDeliveryDatePersian,
            string? desiredDeliveryTime,
            bool useNewAddress,
            string? newAddressTitle,
            string? newFullAddress,
            string? newPostalCode,
            string? newReceiverName,
            string? newReceiverPhone)
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

            // ✅ روش تحویل: در صورت «ارسال با پیک» آدرس تحویل الزامی است (ادمین از طرف مشتری ویرایش می‌کند)
            order.DeliveryMethod = model.DeliveryMethod;
            if (model.DeliveryMethod == DeliveryMethod.Delivery)
            {
                Address? deliveryAddress = null;
                if (model.AddressId.HasValue)
                {
                    deliveryAddress = await _salesDb.Addresses
                        .FirstOrDefaultAsync(a => a.Id == model.AddressId.Value && a.UserId == order.UserId);
                    if (deliveryAddress == null)
                        ModelState.AddModelError("AddressId", "آدرس انتخابی نامعتبر است.");
                }
                else if (useNewAddress &&
                         !string.IsNullOrWhiteSpace(newFullAddress) &&
                         !string.IsNullOrWhiteSpace(newReceiverName) &&
                         !string.IsNullOrWhiteSpace(newReceiverPhone))
                {
                    deliveryAddress = new Address
                    {
                        UserId = order.UserId ?? "",
                        Title = string.IsNullOrWhiteSpace(newAddressTitle) ? "آدرس جدید" : newAddressTitle.Trim(),
                        FullAddress = newFullAddress.Trim(),
                        PostalCode = string.IsNullOrWhiteSpace(newPostalCode) ? null : newPostalCode.Trim(),
                        ReceiverName = newReceiverName.Trim(),
                        ReceiverPhone = newReceiverPhone.Trim(),
                        IsDefault = !await _salesDb.Addresses.AnyAsync(a => a.UserId == order.UserId),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _salesDb.Addresses.Add(deliveryAddress);
                    await _salesDb.SaveChangesAsync();
                }
                else
                {
                    ModelState.AddModelError("AddressId", "برای ارسال با پیک، لطفاً آدرس تحویل را انتخاب کنید یا آدرس جدید وارد کنید.");
                }

                if (deliveryAddress != null)
                {
                    order.AddressId = deliveryAddress.Id;
                    order.ReceiverName = deliveryAddress.ReceiverName;
                    order.ReceiverPhone = deliveryAddress.ReceiverPhone;
                    order.CustomerFullAddress = deliveryAddress.FullAddress;
                    order.CustomerPostalCode = deliveryAddress.PostalCode;
                }
            }
            else
            {
                order.AddressId = null;
                order.ReceiverName = null;
                order.ReceiverPhone = null;
                order.CustomerFullAddress = null;
                order.CustomerPostalCode = null;
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

            if (!string.IsNullOrWhiteSpace(order.UserId))
            {
                ViewData["CakeDeliveryAddresses"] = await _salesDb.Addresses
                    .Where(a => a.UserId == order.UserId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.CreatedAt)
                    .ToListAsync();
            }
            ViewData["CakeDeliveryMethod"] = (int)order.DeliveryMethod;
            ViewData["CakeDeliveryAddressId"] = order.AddressId;

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
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoice(int orderId)
        {
            Invoice invoice;
            try
            {
                invoice = await InvoiceService.GetOrCreateForOrderAsync(_salesDb, _catalogDb, orderId);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = $"فاکتور شماره {invoice.InvoiceNumber} با موفقیت صادر شد.";
            return RedirectToAction("ViewInvoice", new { invoiceId = invoice.Id });
        }

        [Authorize(Roles = "Admin,OrderManager,Owner")]
        [HttpGet]
        public async Task<IActionResult> ViewInvoice(int invoiceId)
        {
            var invoice = await _salesDb.Invoices
                .Include(i => i.Order)
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null) return NotFound();

            // نمایش سهم پرداختی از کیف پول در فاکتور (برای شفافیت مبلغ)
            var walletUsed = await _salesDb.WalletTransactions
                .Where(t => t.OrderId == invoice.OrderId && t.Type == "Purchase" && t.Amount < 0)
                .SumAsync(t => (decimal?)(-t.Amount)) ?? 0m;
            ViewBag.WalletUsed = walletUsed;

            if (!invoice.IsPrinted)
            {
                invoice.IsPrinted = true;
                invoice.PrintedAt = DateTime.UtcNow;
                await _salesDb.SaveChangesAsync();
            }

            return View(invoice);
        }
    }
}