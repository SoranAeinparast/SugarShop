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
        private readonly SugarShop.Web.Services.Sms.SmsService _sms;
        private readonly SugarShop.Web.Services.OrderPricingService _pricingService;
        private readonly ImageStorageService _images;
        private readonly SugarShop.Web.Services.SmsLinkTrackingService _linkTracking;

        public ProfileController(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            SugarShop.Web.Services.Sms.SmsService sms,
            SugarShop.Web.Services.OrderPricingService pricingService,
            ImageStorageService images,
            SugarShop.Web.Services.SmsLinkTrackingService linkTracking)
        {
            _linkTracking = linkTracking;
            _salesDb = salesDb;
            _catalogDb = catalogDb;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _sms = sms;
            _pricingService = pricingService;
            _images = images;
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

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId!) as ApplicationUser;

            var orders = await _salesDb.Orders
                .Where(o => o.UserId == userId && !o.IsInternal)
                .ToListAsync();

            // سفارش‌های کیک سفارشی هم در آمار داشبورد لحاظ می‌شوند
            var cakeOrders = await _salesDb.CustomCakeOrders
                .Where(o => o.UserId == userId)
                .ToListAsync();

            int totalOrders = orders.Count + cakeOrders.Count;
            int pendingPaymentOrders = orders.Count(o => o.OrderStatus == OrderStatus.PendingPayment);
            int deliveredOrders = orders.Count(o => o.OrderStatus == OrderStatus.Delivered)
                + cakeOrders.Count(o => o.Status == CustomCakeOrderStatus.Completed);

            var wallet = await _salesDb.Set<Wallet>().FirstOrDefaultAsync(w => w.UserId == userId);
            decimal walletBalance = wallet?.Balance ?? 0;

            int openTickets = await _salesDb.Set<Ticket>()
                .Where(t => t.UserId == userId && t.Status != TicketStatus.Answered && t.Status != TicketStatus.Closed)
                .CountAsync();

            var lastOrder = await _salesDb.Orders
                .Where(o => o.UserId == userId && !o.IsInternal)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            var lastCakeOrder = cakeOrders
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

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
                LastCakeOrder = lastCakeOrder,
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

            // ایمیل و نام کاربری برای حساب‌های OTP (که خودکار با placeholder ساخته شده‌اند) قابل تغییر است
            if (!string.IsNullOrWhiteSpace(model.Email) &&
                !string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (await _userManager.FindByEmailAsync(model.Email.Trim()) != null)
                    ModelState.AddModelError("Email", "این ایمیل قبلاً توسط کاربر دیگری استفاده شده است.");
            }
            if (!string.IsNullOrWhiteSpace(model.UserName) &&
                !string.Equals(user.UserName, model.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (await _userManager.FindByNameAsync(model.UserName.Trim()) != null)
                    ModelState.AddModelError("UserName", "این نام کاربری قبلاً ثبت شده است.");
            }

            if (ModelState.IsValid)
            {
                if (!string.IsNullOrWhiteSpace(model.Email) &&
                    !string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                    await _userManager.SetEmailAsync(user, model.Email.Trim());
                if (!string.IsNullOrWhiteSpace(model.UserName) &&
                    !string.Equals(user.UserName, model.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
                    await _userManager.SetUserNameAsync(user, model.UserName.Trim());
            }

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

        /// <summary>
        /// ردیف خام لیست سفارش‌های مشتری. هر دو منبع (سفارش فروشگاه و کیک سفارشی) به همین شکل
        /// پروجکت می‌شوند تا فیلتر، جست‌وجو، شمارش و صفحه‌بندی همه در SQL انجام شود و هیچ‌وقت
        /// کل تاریخچه سفارش‌های مشتری (همراه ردیف‌های کالا) بارگذاری نشود.
        /// </summary>
        /// <remarks>
        /// ⚠️ عمداً یک کلاس با مقداردهی اولیه (object initializer) است و نه record با سازنده:
        /// EF Core پروجکشن‌های سازنده‌ای را پروجکشن کلاینتی می‌داند و آنگاه «UNION ALL» دو منبع
        /// ترجمه نمی‌شود؛ با مقداردهی اولیه، کل پروجکشن در SQL اجرا می‌شود.
        /// </remarks>
        private sealed class CustomerOrderRow
        {
            public bool IsCustomCake { get; init; }
            public int Id { get; init; }
            public string OrderCode { get; init; } = "";
            public DateTime CreatedAt { get; init; }
            public OrderStatus? Status { get; init; }
            public PaymentStatus PaymentStatus { get; init; }
            public bool IsPaymentEnabled { get; init; }
            public bool IsDeletable { get; init; }
            public CustomCakeOrderStatus? CakeStatus { get; init; }
            public string? CakeFlavor { get; init; }
            public decimal? CakeFinalPrice { get; init; }
            public decimal? CakeDeliveryFee { get; init; }
            public DeliveryMethod? CakeDeliveryMethod { get; init; }
        }

        /// <summary>تب‌های لیست «سفارشات من» — همان دسته‌بندی معنادار برای هر دو نوع سفارش.</summary>
        private static readonly string[] CustomerOrderTabs = { "all", "unpaid", "active", "done", "canceled" };

        private static string NormalizeCustomerOrderStatus(string? status)
            => !string.IsNullOrWhiteSpace(status) && CustomerOrderTabs.Contains(status) ? status : "all";

        /// <summary>سفارش‌های فروشگاه مشتری (بدون شارژ کیف پول و بدون سفارش موقتِ کیک) + فیلتر وضعیت و جست‌وجو — همه در SQL.</summary>
        private IQueryable<Order> BuildShopOrdersQuery(string userId, string term, string status)
        {
            // فیلتر سفارش‌های داخلی با نشانگر ایندکس‌شده انجام می‌شود (به‌جای مقایسه‌ی متن Notes)
            var query = _salesDb.Orders.AsNoTracking()
                .Where(o => o.UserId == userId && !o.IsInternal);

            query = status switch
            {
                "unpaid" => query.Where(o => o.PaymentStatus == PaymentStatus.Unpaid),
                "active" => query.Where(o => o.OrderStatus == OrderStatus.Paid
                    || o.OrderStatus == OrderStatus.Preparing
                    || o.OrderStatus == OrderStatus.Shipped),
                "done" => query.Where(o => o.OrderStatus == OrderStatus.Delivered),
                "canceled" => query.Where(o => o.OrderStatus == OrderStatus.Cancelled),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(term))
                query = query.Where(o => o.OrderCode.Contains(term));

            return query;
        }

        /// <summary>سفارش‌های کیک سفارشی مشتری + فیلتر وضعیت و جست‌وجو — همه در SQL.</summary>
        private IQueryable<CustomCakeOrder> BuildCakeOrdersQuery(string userId, string term, string status)
        {
            var query = _salesDb.CustomCakeOrders.AsNoTracking().Where(o => o.UserId == userId);

            query = status switch
            {
                // کیک پرداخت‌نشده یعنی هنوز تسویه نشده (مستقل از مرحله تولید)
                "unpaid" => query.Where(o => !o.IsPaid),
                "active" => query.Where(o => o.Status == CustomCakeOrderStatus.Accepted
                    || o.Status == CustomCakeOrderStatus.InProduction
                    || o.Status == CustomCakeOrderStatus.Ready),
                "done" => query.Where(o => o.Status == CustomCakeOrderStatus.Completed),
                "canceled" => query.Where(o => o.Status == CustomCakeOrderStatus.Rejected),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(term))
            {
                // کد کیک در حافظه ساخته می‌شود (CAKE-0012)، پس همان عدد یا طعم/سفارش‌دهنده جست‌وجو می‌شود
                var digits = new string(term.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var cakeId))
                {
                    var capturedId = cakeId;
                    query = query.Where(o => o.Id == capturedId
                        || (o.Flavor != null && o.Flavor.Contains(term)));
                }
                else
                {
                    query = query.Where(o => o.Flavor != null && o.Flavor.Contains(term));
                }
            }

            return query;
        }

        private static IQueryable<CustomerOrderRow> ProjectShopOrders(IQueryable<Order> query) => query.Select(o => new CustomerOrderRow
        {
            IsCustomCake = false,
            Id = o.Id,
            OrderCode = o.OrderCode,
            CreatedAt = o.CreatedAt,
            Status = o.OrderStatus,
            PaymentStatus = o.PaymentStatus,
            IsPaymentEnabled = o.IsPaymentEnabled,
            IsDeletable = o.OrderStatus == OrderStatus.AwaitingReview,
            CakeStatus = (CustomCakeOrderStatus?)null,
            CakeFlavor = (string?)null,
            CakeFinalPrice = (decimal?)null,
            CakeDeliveryFee = (decimal?)null,
            CakeDeliveryMethod = (DeliveryMethod?)null
        });

        private static IQueryable<CustomerOrderRow> ProjectCakeOrders(IQueryable<CustomCakeOrder> query) => query.Select(o => new CustomerOrderRow
        {
            IsCustomCake = true,
            Id = o.Id,
            OrderCode = "", // کد نمایشی (CAKE-XXXX) در حافظه ساخته می‌شود
            CreatedAt = o.CreatedAt,
            Status = (OrderStatus?)null,
            PaymentStatus = o.IsPaid ? PaymentStatus.Succeeded : PaymentStatus.Unpaid,
            IsPaymentEnabled = o.Status == CustomCakeOrderStatus.Accepted && !o.IsPaid && o.FinalPrice != null,
            IsDeletable = o.Status == CustomCakeOrderStatus.Pending,
            CakeStatus = o.Status,
            CakeFlavor = o.Flavor,
            CakeFinalPrice = o.FinalPrice,
            CakeDeliveryFee = o.DeliveryFee,
            CakeDeliveryMethod = o.DeliveryMethod
        });

        /// <summary>
        /// لیست «سفارشات من» — صفحه‌بندی، جست‌وجو، شمارنده تب‌ها و شمارش کل، همه سمت سرور (SQL).
        ///
        /// قبلاً همه سفارش‌ها همراه ردیف‌های کالا لود می‌شد و برای هر سفارش یک کوئری جداگانه مبلغ
        /// محاسبه می‌کرد (N+1)؛ حالا فقط یک صفحه خوانده می‌شود و مبلغ همان صفحه با یک کوئری گروهی
        /// (بدون تکرار ریاضیات پول) از تنها منبع مبلغ می‌آید.
        /// </summary>
        public async Task<IActionResult> Orders(string status = "all", string? search = null, int page = 1)
        {
            const int pageSize = 10;

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            status = NormalizeCustomerOrderStatus(status);
            var term = search?.Trim() ?? string.Empty;

            // ── شمارنده تب‌ها: دو کوئری تجمیعی (GROUP BY) روی کل سفارش‌های منطبق با جست‌وجو،
            //    نه با بارگذاری ردیف‌ها و نه فقط صفحه جاری ──
            var shopCounts = await BuildShopOrdersQuery(userId, term, "all")
                .GroupBy(o => new { o.PaymentStatus, o.OrderStatus })
                .Select(g => new { g.Key.PaymentStatus, g.Key.OrderStatus, Count = g.Count() })
                .ToListAsync();
            var cakeCounts = await BuildCakeOrdersQuery(userId, term, "all")
                .GroupBy(o => new { o.IsPaid, o.Status })
                .Select(g => new { g.Key.IsPaid, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            int CountShop(Func<OrderStatus, bool> predicate) => shopCounts.Where(x => predicate(x.OrderStatus)).Sum(x => x.Count);
            int CountCake(Func<CustomCakeOrderStatus, bool> predicate) => cakeCounts.Where(x => predicate(x.Status)).Sum(x => x.Count);

            var statusCounts = new Dictionary<string, int>
            {
                ["all"] = shopCounts.Sum(x => x.Count) + cakeCounts.Sum(x => x.Count),
                ["unpaid"] = shopCounts.Where(x => x.PaymentStatus == PaymentStatus.Unpaid).Sum(x => x.Count)
                           + cakeCounts.Where(x => !x.IsPaid).Sum(x => x.Count),
                ["active"] = CountShop(s => s == OrderStatus.Paid || s == OrderStatus.Preparing || s == OrderStatus.Shipped)
                           + CountCake(s => s == CustomCakeOrderStatus.Accepted || s == CustomCakeOrderStatus.InProduction || s == CustomCakeOrderStatus.Ready),
                ["done"] = CountShop(s => s == OrderStatus.Delivered) + CountCake(s => s == CustomCakeOrderStatus.Completed),
                ["canceled"] = CountShop(s => s == OrderStatus.Cancelled) + CountCake(s => s == CustomCakeOrderStatus.Rejected)
            };

            // ── یک صفحه از هر دو منبع با یک کوئری (UNION ALL در SQL) و مرتب‌سازی/صفحه‌بندی در دیتابیس ──
            var union = ProjectShopOrders(BuildShopOrdersQuery(userId, term, status))
                .Concat(ProjectCakeOrders(BuildCakeOrdersQuery(userId, term, status)));

            var totalCount = await union.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var currentPage = Math.Clamp(page, 1, totalPages);

            var rows = await union
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ── فقط سفارش‌های همین صفحه (با ردیف‌های کالا) بارگذاری می‌شوند ──
            var shopIds = rows.Where(r => !r.IsCustomCake).Select(r => r.Id).ToList();
            var shopOrders = shopIds.Count == 0
                ? new List<Order>()
                : await _salesDb.Orders.Where(o => shopIds.Contains(o.Id)).Include(o => o.Items).ToListAsync();

            // مبلغ همان صفحه با یک کوئری گروهی از تنها منبع مبلغ (بدون N+1 و بدون محاسبه دستی)
            var pricingById = await _pricingService.ComputeManyAsync(shopOrders, includePayments: false);

            // سیاست ارسال رایگان برای نمایش مبلغ سفارش کیک هم باید همان باشد که در پرداخت اعمال می‌شود
            var freeDeliveryThreshold = await _salesDb.SiteSettings.AsNoTracking()
                .Select(s => (decimal?)s.FreeDeliveryThreshold).FirstOrDefaultAsync() ?? 0m;

            var result = new List<OrderListItemViewModel>(rows.Count);
            foreach (var row in rows) // ترتیب سرور (تازه‌ترین اول) حفظ می‌شود
            {
                if (row.IsCustomCake)
                {
                    decimal cakeGoods = row.CakeFinalPrice ?? 0m;
                    decimal cakeDeliveryFee = row.CakeDeliveryMethod == DeliveryMethod.Delivery
                        ? (row.CakeDeliveryFee ?? 0m) : 0m;
                    if (freeDeliveryThreshold > 0 && cakeGoods >= freeDeliveryThreshold)
                        cakeDeliveryFee = 0m;

                    result.Add(new OrderListItemViewModel
                    {
                        IsCustomCake = true,
                        CustomCakeOrderId = row.Id,
                        OrderCode = $"CAKE-{row.Id:D4}",
                        CreatedAt = row.CreatedAt,
                        TotalFinalPrice = cakeGoods + cakeDeliveryFee,
                        PaymentStatus = row.PaymentStatus,
                        CakeStatus = row.CakeStatus,
                        CakeIsPaid = row.PaymentStatus == PaymentStatus.Succeeded,
                        CakeFlavor = row.CakeFlavor,
                        CakeDeliveryFee = row.CakeDeliveryFee,
                        CakeDeliveryMethod = row.CakeDeliveryMethod,
                        IsPaymentEnabled = row.IsPaymentEnabled,
                        IsDeletable = row.IsDeletable
                    });
                }
                else
                {
                    result.Add(new OrderListItemViewModel
                    {
                        Id = row.Id,
                        OrderCode = row.OrderCode,
                        OrderStatus = row.Status ?? OrderStatus.AwaitingReview,
                        PaymentStatus = row.PaymentStatus,
                        CreatedAt = row.CreatedAt,
                        TotalFinalPrice = pricingById.GetValueOrDefault(row.Id)?.GrandTotal ?? 0m,
                        IsPaymentEnabled = row.IsPaymentEnabled,
                        IsDeletable = row.IsDeletable
                    });
                }
            }

            ViewBag.CurrentStatus = status;
            ViewBag.StatusCounts = statusCounts;
            ViewBag.Search = term;
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;

            return View(result);
        }

        /// <summary>
        /// آدرس کوتاه <c>/o/{id}</c> مخصوص لینک داخل پیامک «سفارش شما آماده پرداخت است» —
        /// چون هر کاراکتر اضافه در آدرس، هزینه پیامک را بالا می‌برد. کاربر را به همان صفحه جزئیات
        /// همان سفارش می‌رساند؛ اگر وارد نشده باشد، به ورود هدایت می‌شود و سپس به همین آدرس برمی‌گردد.
        /// دسترسی به سفارش در همان اکشن اصلی بررسی می‌شود (کاربر فقط سفارش خودش را می‌بیند).
        /// </summary>
        [AllowAnonymous]
        [Route("o/{orderId:int}")]
        public async Task<IActionResult> ShortOrderLink(int orderId)
        {
            // پیامک‌های قدیمی‌تر همین آدرس را دارند؛ بازدیدها هم در اثرسنجی ثبت می‌شود.
            await _linkTracking.RegisterOpenAsync(orderId, SmsLinkKind.WaitingPayment);

            if (User?.Identity?.IsAuthenticated != true) return Challenge();

            return RedirectToAction(nameof(OrderDetails), new { id = orderId });
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
                .Select(x => x.SweetItemId!.Value)
                .Distinct();
            var sweetNames = await _catalogDb.SweetItems
                .Where(x => sweetItemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.TitleFa);

            var productIds = order.Items
                .Where(x => x.ProductId.HasValue)
                .Select(x => x.ProductId!.Value)
                .Distinct();
            var productNames = await _catalogDb.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.TitleFa);

            // در صورت وجود ردیف تکراری برای یک جعبه، فقط جدیدترین ردیف استفاده می‌شود
            var boxInfos = (await _salesDb.BoxFinalInfos
                    .Where(b => b.OrderId == id)
                    .ToListAsync())
                .GroupBy(b => b.BoxTitle)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id).First());

            // ── محاسبات پرداخت مرحله‌ای از منبع واحد مبلغ سفارش ──
            // (همین محاسبه مبلغ دریافتی درگاه را تعیین می‌کند؛ پس اعداد این صفحه همیشه با پرداخت یکی است)
            var pricing = await _pricingService.ComputeAsync(order);

            ViewBag.ProductTotal = pricing.ProductTotal;
            ViewBag.FinalizedBoxTotal = pricing.FinalizedBoxTotal;
            ViewBag.HasUnfinalizedBoxes = pricing.HasUnfinalizedBoxes;
            ViewBag.PaidSoFar = pricing.PaidTotal;
            ViewBag.GrandTotalSoFar = pricing.GrandTotal;
            ViewBag.PayableNow = pricing.PayableNow;
            ViewBag.DiscountAmount = pricing.DiscountAmount;
            ViewBag.DeliveryFee = pricing.DeliveryFee;
            ViewBag.Payments = await _salesDb.Payments
                .Where(p => p.OrderId == order.Id && p.PaymentStatus == PaymentStatus.Succeeded)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

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
        public async Task<IActionResult> PayOrder(int orderId, bool confirmed = false)
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

            // ── سفارش جعبه‌ای: پرداخت مرحله دوم است و مبلغش از وزن‌کشی می‌آید،
            // پس پیش از درگاه، صفحه «تأیید و پرداخت» با ریز وزن و قیمت هر ردیف نشان داده می‌شود.
            // (دکمه همان صفحه با confirmed=true برمی‌گردد و به درگاه می‌رود.)
            if (!confirmed)
            {
                var hasBoxRows = await _salesDb.OrderItems
                    .AnyAsync(i => i.OrderId == order.Id && i.ItemType == OrderItemType.SweetItem && i.BoxTitle != null);

                if (hasBoxRows)
                    return RedirectToAction("Confirm", "Payment", new { orderId = order.Id });
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

            ViewData["CakeDeliveryAddresses"] = await _salesDb.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
            ViewData["CakeDeliveryMethod"] = (int)order.DeliveryMethod;
            ViewData["CakeDeliveryAddressId"] = order.AddressId;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomCakeOrder(int id,
                                                             CustomCakeOrder model,
                                                             string? desiredDeliveryDatePersian,
                                                             string? desiredDeliveryTime,
                                                             IFormFile? sampleImage,
                                                             IFormFile? printImage,
                                                             bool useNewAddress,
                                                             string? newAddressTitle,
                                                             string? newFullAddress,
                                                             string? newPostalCode,
                                                             string? newReceiverName,
                                                             string? newReceiverPhone)
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

            // ✅ روش تحویل: در صورت «ارسال با پیک» آدرس تحویل الزامی است
            order.DeliveryMethod = model.DeliveryMethod;
            if (model.DeliveryMethod == DeliveryMethod.Delivery)
            {
                Address? deliveryAddress = null;
                if (model.AddressId.HasValue)
                {
                    deliveryAddress = await _salesDb.Addresses
                        .FirstOrDefaultAsync(a => a.Id == model.AddressId.Value && a.UserId == userId);
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
                        UserId = userId!,
                        Title = string.IsNullOrWhiteSpace(newAddressTitle) ? "آدرس جدید" : newAddressTitle!.Trim(),
                        FullAddress = newFullAddress!.Trim(),
                        PostalCode = string.IsNullOrWhiteSpace(newPostalCode) ? null : newPostalCode.Trim(),
                        ReceiverName = newReceiverName!.Trim(),
                        ReceiverPhone = newReceiverPhone!.Trim(),
                        IsDefault = !await _salesDb.Addresses.AnyAsync(a => a.UserId == userId),
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
            if (sampleImage != null && sampleImage.Length > 0)
            {
                var (savedSamplePath, sampleImageError) = await _images.SaveImageAsync(sampleImage, "cake_sample", ImageUploadValidator.MaxCakeImageBytes);
                if (sampleImageError != null)
                {
                    ModelState.AddModelError("sampleImage", sampleImageError);
                }
                else if (savedSamplePath != null)
                {
                    _images.DeleteIfExists(order.SampleImagePath);
                    order.SampleImagePath = savedSamplePath;
                }
            }
            if (printImage != null && printImage.Length > 0)
            {
                var (savedPrintPath, printImageError) = await _images.SaveImageAsync(printImage, "cake_print", ImageUploadValidator.MaxCakeImageBytes);
                if (printImageError != null)
                {
                    ModelState.AddModelError("printImage", printImageError);
                }
                else if (savedPrintPath != null)
                {
                    _images.DeleteIfExists(order.PrintImagePath);
                    order.PrintImagePath = savedPrintPath;
                }
            }

            ViewData["CakeDeliveryAddresses"] = await _salesDb.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
            ViewData["CakeDeliveryMethod"] = (int)order.DeliveryMethod;
            ViewData["CakeDeliveryAddressId"] = order.AddressId;

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

            // اگر هنوز قیمت نهایی توسط فروشگاه ثبت نشده باشد، اجازه پرداخت وجود ندارد
            if (!cakeOrder.FinalPrice.HasValue)
            {
                TempData["Error"] = "قیمت نهایی این سفارش هنوز توسط فروشگاه اعلام نشده است. لطفاً بعداً مراجعه کنید.";
                return RedirectToAction("CustomCakeOrderDetails", new { id });
            }

            // 🔒 اگر سفارش موقتِ پرداخت‌نشده‌ای برای همین کیک وجود دارد، همان ادامه داده میشود؛
            // در غیر این صورت هر بار کلیک/رفرش، یک سفارش موقت جدید و یک پرداخت جدید می‌ساخت
            // (امکان پرداخت دوباره برای یک کیک واحد).
            var cakePaymentNote = OrderNotes.ForCustomCakeOrder(cakeOrder.Id);
            var pendingTempOrder = await _salesDb.Orders
                .Where(o => o.UserId == userId
                    && o.Notes == cakePaymentNote
                    && o.PaymentStatus != PaymentStatus.Succeeded)
                .OrderByDescending(o => o.Id)
                .FirstOrDefaultAsync();

            if (pendingTempOrder != null)
                return RedirectToAction("RequestPayment", "Payment", new { orderId = pendingTempOrder.Id });

            var tempOrder = new Order
            {
                OrderCode = "CAKE-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                UserId = userId,
                OrderStatus = OrderStatus.PendingPayment,
                PaymentStatus = PaymentStatus.Unpaid,
                TotalAmountSnapshot = cakeOrder.FinalPrice!.Value,
                // مبلغ نمایشی سفارش هم شامل هزینه پیک است تا با مبلغی که محاسبه و دریافت میشود یکی باشد
                FinalTotalAmount = cakeOrder.FinalPrice!.Value
                    + (cakeOrder.DeliveryMethod == DeliveryMethod.Delivery ? (cakeOrder.DeliveryFee ?? 0) : 0),
                DeliveryFeeSnapshot = cakeOrder.DeliveryMethod == DeliveryMethod.Delivery ? (cakeOrder.DeliveryFee ?? 0) : 0,
                CustomerName = !string.IsNullOrWhiteSpace(cakeOrder.ReceiverName) ? cakeOrder.ReceiverName : (User.Identity?.Name ?? "کاربر"),
                CustomerPhone = cakeOrder.ReceiverPhone ?? "",
                CustomerFullAddress = cakeOrder.CustomerFullAddress ?? "",
                CustomerPostalCode = cakeOrder.CustomerPostalCode ?? "",
                CustomerAddressId = cakeOrder.AddressId,
                DeliveryDate = cakeOrder.DesiredDeliveryDateTime?.Date,
                DeliveryTime = cakeOrder.DesiredDeliveryDateTime?.TimeOfDay,
                DeliveryMethod = cakeOrder.DeliveryMethod,
                CreatedAt = DateTime.UtcNow,
                IsPaymentEnabled = true,
                Notes = OrderNotes.ForCustomCakeOrder(cakeOrder.Id)
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
                wallet = new Wallet { UserId = userId!, Balance = 0 };
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
                UserId = userId!,
                Subject = subject,
                Message = message,
                Status = TicketStatus.Open,
                CreatedAt = DateTime.UtcNow
            };
            _salesDb.Set<Ticket>().Add(ticket);
            await _salesDb.SaveChangesAsync();
            // ── پیامک «تیکت شما دریافت شد» ──
            try { await _sms.NotifyTicketReceivedAsync(userId!, ticket.Id); }
            catch { /* پیامک نباید ثبت تیکت را متوقف کند */ }
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