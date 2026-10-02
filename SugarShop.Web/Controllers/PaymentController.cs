using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.ViewModels;
using SugarShop.Web.Helpers;
using SugarShop.Web.Extensions;
using MD.PersianDateTime;
using SugarShop.Infrastructure.Services;
using SugarShop.Web.Services;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private const string PendingAuthorityPrefix = "pending:";
        private readonly ZibalPaymentService _zibalPaymentService;
        private readonly SugarShopSalesDbContext _context;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly InventoryService _inventoryService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(SugarShopSalesDbContext context,
                                 SugarShopCatalogDbContext catalogDb,
                                 UserManager<ApplicationUser> userManager,
                                 IConfiguration configuration,
                                 ZibalPaymentService zibalPaymentService,
                                 InventoryService inventoryService,
                                 SugarShop.Web.Services.OrderPricingService pricingService,
                                 ILogger<PaymentController> logger,
                                 SugarShop.Web.Services.Sms.SmsService smsService,
                                 SugarShop.Web.Services.PaymentStatementPdfService statementPdf,
                                 SugarShop.Web.Services.StatementLinkService statementLinks,
                                 SugarShop.Web.Services.SmsLinkTrackingService linkTracking)
        {
            _context = context;
            _catalogDb = catalogDb;
            _userManager = userManager;
            _configuration = configuration;
            _zibalPaymentService = zibalPaymentService;
            _inventoryService = inventoryService;
            _pricingService = pricingService;
            _logger = logger;
            _sms = smsService;
            _statementPdf = statementPdf;
            _statementLinks = statementLinks;
            _linkTracking = linkTracking;
        }

        private readonly SugarShop.Web.Services.OrderPricingService _pricingService;

        private readonly SugarShop.Web.Services.Sms.SmsService _sms;

        private readonly SugarShop.Web.Services.PaymentStatementPdfService _statementPdf;

        private readonly SugarShop.Web.Services.StatementLinkService _statementLinks;

        private readonly SugarShop.Web.Services.SmsLinkTrackingService _linkTracking;

    /// <summary>
    /// خلاصه مالی سفارش از تنها منبع محاسبه مبلغ در برنامه (OrderPricingService).
    /// این کنترلر هیچ محاسبه مالی مستقیمی انجام نمی‌دهد تا مبلغی که از درگاه دریافت می‌شود
    /// همیشه با مبلغی که در پنل مدیریت و پروفایل مشتری نمایش داده می‌شود یکی باشد.
    /// </summary>
    private Task<SugarShop.Web.Services.OrderPricing> GetPricingAsync(Order order)
        => _pricingService.ComputeAsync(order);

    /// <summary>
    /// هدایت کاربر پس از بازگشت از درگاه.
    /// اگر نشست کاربر منقضی شده باشد (یا درخواست از سوی شخص دیگری باشد) فقط صاحب سفارش
    /// می‌تواند صفحه سفارش را ببیند؛ بقیه به صفحه اصلی می‌روند، در حالی که نتیجه پرداخت
    /// در خود سفارش ثبت شده است.
    /// </summary>
    private async Task<IActionResult> RedirectAfterCallback(Order order, bool isWalletRecharge, bool success)
    {
        bool isOwner = !string.IsNullOrEmpty(order.UserId)
            && order.UserId == _userManager.GetUserId(User);

        if (isWalletRecharge)
        {
            if (!isOwner) return RedirectToAction("Recharge", "Wallet");
            return success ? RedirectToAction("Wallet", "Profile")
                           : RedirectToAction("Recharge", "Wallet");
        }

        if (!isOwner)
        {
            // مشتری بدون ورود پرداخت کرده است (از لینک اختصاصی پیامکی /s/{token}): نتیجه پرداخت
            // باید به خودش نشان داده شود، پس به همان سند اختصاصی برمیگردد نه صفحه اصلی.
            // اگر لینک معتبری برای این سفارش نباشد، رفتار قبلی (صفحه اصلی) حفظ میشود.
            var token = await _statementLinks.FindActiveTokenForOrderAsync(order.Id, order.UserId);
            if (!string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(PublicStatement), new { token });

            return RedirectToAction("Index", "Home");
        }

        if (OrderNotes.TryParseCustomCakeOrderId(order.Notes).HasValue)
            return RedirectToAction("CustomCakeOrders", "Profile");

        return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
    }

    /// <summary>نوع درگاه فعال از تنظیمات دیتابیس (اگر هیچ درگاهی فعال نباشد، پیش‌فرض Zibal).</summary>
    private async Task<string> ResolveActiveGatewayTypeAsync()
    {
        try
        {
            var active = await _context.PaymentGateways.AsNoTracking()
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder)
                .Select(g => g.GatewayType)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(active) ? "Zibal" : active!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "خواندن درگاه فعال از دیتابیس ناموفق بود؛ مقدار پیش‌فرض زیبال استفاده میشود");
            return "Zibal";
        }
    }

    /// <summary>
        /// آدرس کوتاه <c>/p/{id}</c> برای پیامک «سفارش شما آماده پرداخت است»: مشتری از پیامک
        /// مستقیم روی ریز مبلغ می‌نشیند و با یک کلیک به درگاه می‌رود (چون هر کاراکتر آدرس، هزینه پیامک است).
        ///
        /// چرا یک اکشن جدا و نه <c>[Route]</c> روی خودِ «تأیید و پرداخت»؟ چون در ASP.NET Core،
        /// اکشنی که مسیر صفتی دارد از مسیرهای قراردادی حذف می‌شود و آدرس قدیمی
        /// <c>Payment/Confirm?orderId=…</c> (که در تاریخچه مرورگر مشتریان و لینک‌های قبلی است)
        /// از کار می‌افتاد. اینجا فقط یک گام هدایت اضافه می‌شود و آدرس قبلی سالم می‌ماند.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        [Route("p/{orderId:int}")]
        public async Task<IActionResult> ShortPayLink(int orderId)
        {
            // آمار باز شدن لینک *پیش از ورود* ثبت می‌شود؛ وگرنه مشتری‌هایی که لینک را باز
            // می‌کنند ولی وارد نمی‌شوند در گزارش دیده نمی‌شدند و اثر پیامک کمتر از واقع می‌شد.
            // این مسیر خودش هیچ مبلغی نشان نمی‌دهد (آدرس /p/{id} قابل حدس است)؛ فقط آمار و هدایت.
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            await _linkTracking.RegisterOpenAsync(orderId, SmsLinkKind.WaitingPayment);

            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction(nameof(Confirm), new { orderId });

            // ── چرا اینجا توکن ساخته نمی‌شود و فقط «ورود» درخواست می‌شود؟ ──
            // چون این آدرس قابل حدس است (/p/1 ، /p/2 …)؛ اگر هر بازدیدکننده ناشناس با حدس زدن
            // شماره سفارش، توکن لینک اختصاصی می‌گرفت، هر کسی می‌توانست صورت‌حساب بقیه را ببیند.
            // پس «بدون ورود» فقط از مسیر توکن‌دار و حدس‌زدنی‌نبودن صورت می‌گیرد: پیامک‌های جدید
            // بهجای این آدرس، آدرس /s/{token}?pay=1 را می‌فرستند (ساخته‌شده در SmsService) که
            // توکن را از دیتابیس می‌خواند و چیزی نمی‌سازد.
            // این آدرس فقط برای پیامک‌های قدیمی که همین حالا در گوشی مشتریان است باقی می‌ماند.
            return Challenge();
        }

        /// <summary>
        /// صفحه «تأیید و پرداخت»: پیش از هدایت به درگاه، ریز وزن و قیمت هر ردیف جعبه و
        /// سهم هر بخش در مبلغ، به مشتری نشان داده می‌شود تا بداند مبلغ از کجا آمده است.
        /// اعداد از همان OrderPricingService و مقادیر ثبت‌شده روی ردیف‌های سفارش می‌آید،
        /// پس با مبلغی که درگاه دریافت می‌کند یکی است.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Confirm(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return NotFound();

            if (!order.IsPaymentEnabled || order.PaymentStatus != PaymentStatus.Unpaid)
            {
                TempData["Info"] = "این سفارش در این مرحله قابل پرداخت نیست.";
                return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            var pricing = await GetPricingAsync(order);
            if (pricing.PayableNow <= 0)
            {
                TempData["Info"] = "هیچ مبلغی برای پرداخت باقی نمانده است.";
                return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            return View(await BuildStagePaymentAsync(order, pricing));
        }

        /// <summary>
        /// ساخت ریز مبلغ یک مرحله پرداخت — منطق مشترک بین صفحه «تأیید و پرداخت»،
        /// نسخه چاپی/PDF و خروجی اکسل؛ پس هر سه سند همیشه یک عدد نشان می‌دهند.
        /// </summary>
        private async Task<StagePaymentViewModel> BuildStagePaymentAsync(Order order, SugarShop.Web.Services.OrderPricing pricing)
        {
            var items = order.Items ?? new List<OrderItem>();

            // نام شیرینی‌ها (قیمت‌ها از مقادیر ثبت‌شده روی خود ردیف خوانده می‌شود تا با زمان وزن‌کشی یکی باشد)
            var sweetIds = items.Where(i => i.SweetItemId.HasValue).Select(i => i.SweetItemId!.Value).Distinct().ToList();
            var sweetNames = await _catalogDb.SweetItems.AsNoTracking()
                .Where(s => sweetIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.TitleFa);

            var productIds = items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
            var productNames = await _catalogDb.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.TitleFa);

            var boxInfos = (await _context.BoxFinalInfos.AsNoTracking()
                    .Where(b => b.OrderId == order.Id)
                    .ToListAsync())
                .GroupBy(b => b.BoxTitle)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id).First());

            var store = await _context.SiteSettings.AsNoTracking().FirstOrDefaultAsync();

            // نام دارنده‌ی حساب (مشتری) و نام گیرنده‌ی سفارش را جدا نگه می‌داریم تا در سند
            // «مشتری» و «گیرنده» هر دو همانی باشند که واقعاً هستند — قبلاً فقط نام گیرنده
            // (order.CustomerName = نام آدرس تحویل) به‌عنوان «مشتری» چاپ می‌شد.
            string buyerName = order.CustomerName;
            if (!string.IsNullOrWhiteSpace(order.UserId))
            {
                var buyer = await _userManager.FindByIdAsync(order.UserId!);
                if (buyer != null && !string.IsNullOrWhiteSpace(buyer.FullName))
                    buyerName = buyer.FullName;
            }

            var vm = new StagePaymentViewModel
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                OrderDate = new PersianDateTime(order.CreatedAt).ToString("yyyy/MM/dd"),
                StoreName = string.IsNullOrWhiteSpace(store?.SiteTitle) ? "شیرینی سرای تک" : store!.SiteTitle!,
                StorePhone = store?.Phone ?? "",
                CustomerName = buyerName,
                ReceiverName = order.CustomerName,
                ReceiverPhone = order.CustomerPhone,
                PrintDate = new PersianDateTime(IranClock.Now).ToString("yyyy/MM/dd HH:mm"),
                DeliveryMethodText = order.DeliveryMethod == DeliveryMethod.Pickup ? "🏪 دریافت در محل" : "🛵 ارسال با پیک",
                ProductTotal = pricing.ProductTotal,
                FinalizedBoxTotal = pricing.FinalizedBoxTotal,
                GoodsTotal = pricing.GoodsTotal,
                StoredDeliveryFee = pricing.StoredDeliveryFee,
                DeliveryFee = pricing.DeliveryFee,
                IsDeliveryFree = pricing.IsDeliveryFree,
                DiscountAmount = pricing.DiscountAmount,
                GrandTotal = pricing.GrandTotal,
                PaidTotal = pricing.PaidTotal,
                PayableNow = pricing.PayableNow,
                HasUnfinalizedBoxes = pricing.HasUnfinalizedBoxes,
                // دکمه پرداخت فقط وقتی معنا دارد که سفارش در همین مرحله قابل پرداخت باشد.
                // همین یک پرچم، هم صفحه «تأیید و پرداخت» پس از ورود و هم لینک اختصاصی پیامکی
                // را تغذیه می‌کند تا در هیچ حالتی دکمه‌ای که کار نمی‌کند نشان داده نشود.
                CanPayNow = order.IsPaymentEnabled
                    && order.PaymentStatus == PaymentStatus.Unpaid
                    && pricing.PayableNow > 0
            };

            // ── ریز ردیف‌های هر جعبه ──
            var boxGroups = items
                .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                .GroupBy(i => i.BoxTitle!)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            var boxNumber = 0;
            foreach (var group in boxGroups)
            {
                boxNumber++;
                boxInfos.TryGetValue(group.Key, out var info);
                var rows = new List<StagePaymentLine>();

                foreach (var row in group.OrderBy(r => r.Id))
                {
                    var quantity = row.Quantity < 1 ? 1 : row.Quantity;
                    var weight = row.WeightSnapshotGrams ?? 0;
                    var unitPrice = row.UnitPriceSnapshot > 0
                        ? row.UnitPriceSnapshot
                        : row.TotalPriceSnapshot / quantity; // ردیف‌های قدیمی که قیمت واحدشان ثبت نشده

                    rows.Add(new StagePaymentLine
                    {
                        Name = row.SweetItemId.HasValue && sweetNames.TryGetValue(row.SweetItemId.Value, out var sweetName)
                            ? sweetName
                            : $"شیرینی (کد {row.SweetItemId})",
                        Quantity = quantity,
                        WeightGrams = weight > 0 ? weight : null,
                        UnitPrice = unitPrice,
                        TotalPrice = row.TotalPriceSnapshot,
                        // وزن/قیمت ردیف تا وقتی جعبه رسماً وزن‌کشی و ثبت نشده باشد، تقریبی است
                        IsApproximate = info == null || weight <= 0
                    });
                }

                vm.Boxes.Add(new StagePaymentBox
                {
                    BoxTitle = group.Key,
                    BoxNumber = boxNumber,
                    BoxCount = boxGroups.Count,
                    Rows = rows,
                    RowsWeightGrams = rows.Sum(r => r.WeightGrams ?? 0),
                    RowsPrice = rows.Sum(r => r.TotalPrice),
                    FinalWeightGrams = info?.FinalWeightGrams,
                    FinalPrice = info?.FinalPrice,
                    IsFinalized = info != null
                });
            }

            // ── محصولات قیمت‌ثابت ──
            vm.Products = items
                .Where(i => i.ItemType == OrderItemType.Product)
                .OrderBy(i => i.Id)
                .Select(i => new StagePaymentLine
                {
                    Name = i.ProductId.HasValue && productNames.TryGetValue(i.ProductId.Value, out var productName)
                        ? productName
                        : "محصول",
                    Quantity = i.Quantity < 1 ? 1 : i.Quantity,
                    WeightGrams = i.WeightSnapshotGrams,
                    UnitPrice = i.UnitPriceSnapshot,
                    TotalPrice = i.TotalPriceSnapshot
                })
                .ToList();

            // ── پرداخت‌های موفق قبلی (شفافیت مبلغ باقی‌مانده) ──
            vm.PreviousPayments = (await _context.Payments.AsNoTracking()
                    .Where(p => p.OrderId == order.Id && p.PaymentStatus == PaymentStatus.Succeeded)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync())
                .Select(p => new StagePaymentPayment
                {
                    Amount = p.Amount,
                    Date = new PersianDateTime(p.CreatedAt).ToString("yyyy/MM/dd HH:mm"),
                    Provider = string.IsNullOrWhiteSpace(p.Provider) ? "درگاه" : p.Provider
                })
                .ToList();

            return vm;
        }

        /// <summary>
        /// نسخه چاپی/PDF صورت‌حساب مرحله پرداخت: همان ریز وزن و قیمت صفحه تأیید، در قالب یک برگه A4
        /// تا مشتری آن را چاپ کند یا به‌صورت PDF ذخیره کند. چون سند خود مشتری است، پس از پرداخت هم
        /// قابل مشاهده و چاپ می‌ماند.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Statement(int orderId)
        {
            var (order, error) = await LoadOwnStatementOrderAsync(orderId);
            if (order == null) return error!;

            var pricing = await GetPricingAsync(order);
            return View(await BuildStagePaymentAsync(order, pricing));
        }

        /// <summary>
        /// لینک اختصاصی و موقت صورت‌حساب (/s/{token}): همان سند کامل صورت‌حساب، ولی بدون نیاز به ورود.
        /// توکن در پیامک به مشتری می‌رود، فقط «خواندن سند همان سفارش» را مجاز می‌کند، چند روز اعتبار
        /// دارد و هیچ راهی برای تغییر سفارش یا پرداخت از این مسیر وجود ندارد.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("/s/{token}")]
        public async Task<IActionResult> PublicStatement(string token, int? pay = null)
        {
            var (order, link) = await LoadPublicStatementOrderAsync(token);
            if (order == null || link == null) return PublicLinkProblem();

            await _statementLinks.RegisterOpenAsync(link.Id);

            // اگر مشتری از لینک پرداخت پیامکی (/p/{id} یا /o/{id}) به اینجا هدایت شده باشد،
            // همان بازدید به نام پیامک «آماده پرداخت» ثبت می‌شود و در ستون «لینک صورت‌حساب»
            // دوباره شمرده نمی‌شود؛ وگرنه آمار اثر پیامک‌ها دو بار حساب می‌شد.
            if (pay == 1)
                await _linkTracking.RegisterOpenAsync(order.Id, SmsLinkKind.WaitingPayment);
            else
                await _linkTracking.RegisterOpenAsync(order.Id, SmsLinkKind.Statement);

            var pricing = await GetPricingAsync(order);
            var vm = await BuildStagePaymentAsync(order, pricing);
            ApplyPublicLink(vm, token, link.ExpiresAt);
            vm.PayFromPublicLink = pay == 1;

            NoStore();
            return View("Statement", vm);
        }

        /// <summary>
        /// پرداخت بدون ورود از مسیر لینک اختصاصی: <c>/s/{token}/pay</c>.
        ///
        /// چرا این مسیر لازم است؟ چون هدف این بود که «ورود» از وسط مسیر پرداخت برداشته شود؛
        /// مشتری از پیامک، ریز وزن و قیمت را می‌بیند و همان‌جا پرداخت می‌کند. احراز هویت اینجا با
        /// توکن اختصاصی سفارش انجام می‌شود (۱۲۸ بیت تصادفی، گره‌خورده به سفارش و صاحبش، با انقضای
        /// چندروزه). این توکن فقط اجازه «پرداخت همین سفارش» را می‌دهد: نه تغییر سفارش، نه دیدن
        /// سفارش دیگر، نه برداشت یا انتقال پول. مبلغ هم دوباره سمت سرور از OrderPricingService
        /// حساب می‌شود؛ پس فرقی با پرداخت پس از ورود ندارد.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("/s/{token}/pay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublicPay(string token)
        {
            var (order, link) = await LoadPublicStatementOrderAsync(token);
            if (order == null || link == null) return PublicLinkProblem();

            // شارژ کیف پول و سفارش‌های داخلی از این مسیر پرداخت نمی‌شوند (سند ندارند)
            if (order.Notes == OrderNotes.WalletRecharge) return PublicLinkProblem();

            if (!order.IsPaymentEnabled || order.PaymentStatus != PaymentStatus.Unpaid)
            {
                TempData["Info"] = "این سفارش در این مرحله قابل پرداخت نیست.";
                return RedirectToAction(nameof(PublicStatement), new { token });
            }

            return await StartGatewayPaymentAsync(order, isWalletRecharge: false,
                failureRedirect: () => RedirectToAction(nameof(PublicStatement), new { token }));
        }

        /// <summary>دانلود PDF صورت‌حساب با همان لینک اختصاصی پیامکی (بدون ورود).</summary>
        [AllowAnonymous]
        [HttpGet("/s/{token}/pdf")]
        public async Task<IActionResult> PublicStatementPdf(string token)
        {
            var (order, link) = await LoadPublicStatementAsync(token);
            if (order == null || link == null) return PublicLinkProblem();

            var pricing = await GetPricingAsync(order);
            var vm = await BuildStagePaymentAsync(order, pricing);
            ApplyPublicLink(vm, token, link.ExpiresAt);

            NoStore();
            var pdf = StatementPdfFile(vm);
            return pdf == null ? PublicLinkProblem() : pdf;
        }

        /// <summary>
        /// صحت توکن را می‌سنجد و سفارش متناظرش را بدون تغییر مسیر بازدید می‌کند؛
        /// هر بازدید در جدول StatementLinks ثبت می‌شود (برای سنجش اثر پیامک).
        /// </summary>
        private async Task<(Order? Order, StatementLink? Link)> LoadPublicStatementAsync(string? token)
        {
            var (order, link) = await LoadPublicStatementOrderAsync(token);
            if (order != null && link != null) await _statementLinks.RegisterOpenAsync(link.Id);
            return (order, link);
        }

        /// <summary>
        /// سفارش قابل نمایش با توکن اختصاصی. اگر توکن نامعتبر/منقضی باشد، سفارش وجود نداشته باشد،
        /// سند نداشته باشد، یا سفارش به کاربر دیگری منتقل شده باشد، null برمی‌گردد.
        /// </summary>
        private async Task<(Order? Order, StatementLink? Link)> LoadPublicStatementOrderAsync(string? token)
        {
            var link = await _statementLinks.FindUsableAsync(token);
            if (link == null) return (null, null);

            var order = await _context.Orders.AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == link.OrderId);

            if (order == null) return (null, null);
            if (order.Notes == OrderNotes.WalletRecharge || order.Items == null || order.Items.Count == 0) return (null, null);

            // لایه دفاعی: لینک فقط برای همان کاربری معتبر است که سفارش را ثبت کرده
            if (!string.IsNullOrEmpty(link.UserId) && order.UserId != link.UserId) return (null, null);

            return (order, link);
        }

        /// <summary>صفحه‌ی راهنمای لینک نامعتبر/منقضی (بدون افشای اطلاعات سفارش).</summary>
        private IActionResult PublicLinkProblem() => View("StatementLinkExpired");

        /// <summary>مشخصات لینک اختصاصی را روی مدل می‌گذارد تا ویو نسخه عمومی سند را بسازد.</summary>
        private static void ApplyPublicLink(StagePaymentViewModel vm, string token, DateTime expiresAtUtc)
        {
            vm.PublicToken = token;
            vm.LinkExpiresAtText = new PersianDateTime(expiresAtUtc + IranClock.Offset).ToString("yyyy/MM/dd");
        }

        /// <summary>سند شخصی است: نه در کش مرورگر/پروکسی بماند و نه ایندکس شود.</summary>
        private void NoStore()
        {
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";
            Response.Headers.Pragma = "no-cache";
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        }

        /// <summary>
        /// دانلود صورت‌حساب مرحله پرداخت به‌شكل فایل PDF واقعی (بدون نیاز به پنجره چاپ مرورگر).
        /// محتوا دقیقاً از همان <see cref="BuildStagePaymentAsync"/> می‌آید که صفحه تأیید می‌سازد؛
        /// پس صفحه و PDF یک عدد نشان می‌دهند.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> StatementPdf(int orderId)
        {
            var (order, error) = await LoadOwnStatementOrderAsync(orderId);
            if (order == null) return error!;

            var pricing = await GetPricingAsync(order);
            var vm = await BuildStagePaymentAsync(order, pricing);

            var pdf = StatementPdfFile(vm);
            if (pdf == null)
            {
                TempData["Error"] = "تولید فایل PDF ناموفق بود؛ لطفاً دوباره تلاش کنید.";
                return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            return pdf;
        }

        /// <summary>
        /// ساخت فایل PDF صورت‌حساب از یک ریز مبلغ آماده — مشترک بین مسیر وروددار و مسیر لینک
        /// اختصاصی پیامکی، تا هر دو یک سند یکسان بدهند. null یعنی تولید ناموفق بود (در لاگ ثبت شده).
        /// </summary>
        private FileContentResult? StatementPdfFile(StagePaymentViewModel vm)
        {
            byte[] pdf;
            try
            {
                pdf = _statementPdf.Build(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "تولید فایل PDF صورت‌حساب سفارش {OrderId} ناموفق بود", vm.OrderId);
                return null;
            }

            // کد سفارش در نام فایل فقط با حروف و ارقام انگلیسی نوشته می‌شود (امنیت نام فایل)
            var safeCode = new string(vm.OrderCode.Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrEmpty(safeCode)) safeCode = vm.OrderId.ToString();

            return File(pdf, "application/pdf", $"Statement_{safeCode}_{IranClock.Now:yyyyMMdd_HHmm}.pdf");
        }

        /// <summary>
        /// بارگذاری سفارش برای سند صورت‌حساب مشتری: فقط سفارش خودش، و فقط اگر ردیف فروش داشته باشد
        /// (سفارش‌های داخلی مثل شارژ کیف پول سند ندارند). خروجی null یعنی پاسخ آماده برای بازگشت است.
        /// </summary>
        private async Task<(Order? Order, IActionResult? Error)> LoadOwnStatementOrderAsync(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
                return (null, NotFound());

            if (order.Notes == OrderNotes.WalletRecharge || order.Items == null || order.Items.Count == 0)
            {
                TempData["Info"] = "برای این سفارش صورت‌حساب پرداخت صادر نشده است.";
                return (null, RedirectToAction("OrderDetails", "Profile", new { id = order.Id }));
            }

            return (order, null);
        }

    [HttpGet]
        public async Task<IActionResult> RequestPayment(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return NotFound();

            bool isWalletRecharge = order.Notes == OrderNotes.WalletRecharge;
            if (isWalletRecharge)
            {
                if (order.PaymentStatus != PaymentStatus.Unpaid)
                    return BadRequest("پرداخت برای این سفارش امکان‌پذیر نیست.");
            }
            else
            {
                if (!order.IsPaymentEnabled || order.PaymentStatus != PaymentStatus.Unpaid)
                    return BadRequest("پرداخت برای این سفارش امکان‌پذیر نیست.");
            }

            return await StartGatewayPaymentAsync(order, isWalletRecharge,
                failureRedirect: () => isWalletRecharge
                    ? RedirectToAction("Recharge", "Wallet")
                    : RedirectToAction("OrderDetails", "Profile", new { id = order.Id }));
        }

        /// <summary>
        /// شروع پرداخت اینترنتی: ساخت پرداخت مرحله‌ای در درگاه و هدایت مشتری به صفحه بانک.
        ///
        /// منطق مشترک «پرداخت پس از ورود» (RequestPayment) و «پرداخت بدون ورود با لینک اختصاصی
        /// پیامکی» (PublicPay) است تا مبلغ، بررسی درگاه فعال، ضد تکرار و پیام‌های خطا در هر دو
        /// مسیر یکی باشد. مبلغ در هر صورت سمت سرور از OrderPricingService محاسبه می‌شود.
        /// </summary>
        private async Task<IActionResult> StartGatewayPaymentAsync(Order order, bool isWalletRecharge, Func<IActionResult> failureRedirect)
        {
            // ── پرداخت مرحله‌ای: مبلغ = کل تاکنون نهایی‌شده − پرداخت‌های قبلی ──
            // سفارش ترکیبی: محصولات قیمت‌ثابت را همان لحظه می‌پردازد؛ جعبه‌ها پس از وزن‌کشی اضافه می‌شوند.
            long amountInTomans;
            if (isWalletRecharge)
            {
                amountInTomans = (long)order.TotalAmountSnapshot;
            }
            else
            {
                var pricing = await GetPricingAsync(order);
                if (pricing.PayableNow <= 0)
                {
                    TempData["Info"] = "هیچ مبلغی برای پرداخت باقی نمانده است.";
                    return failureRedirect();
                }
                amountInTomans = (long)Math.Ceiling(pricing.PayableNow);
            }
            long amountInRials = amountInTomans * 10;

            // درگاه فعال سایت باید همان درگاهی باشد که به این نسخه متصل است.
            var activeGateway = await ResolveActiveGatewayTypeAsync();
            if (!string.Equals(activeGateway, "Zibal", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Payment blocked: active gateway '{Gateway}' is not wired in this build (only Zibal is).", activeGateway);
                TempData["Error"] = $"درگاه فعال «{activeGateway}» در این نسخه به پرداخت متصل نیست. لطفاً از پنل مدیریت، درگاه زیبال را فعال کنید.";
                return failureRedirect();
            }

            // ── اگر همین سفارش یک پرداخت در جریان دارد، همان ادامه داده میشود ──
            // (جلوگیری از ساخت پرداخت تکراری وقتی کاربر دکمه پرداخت را دوبار میزند یا صفحه را رفرش میکند)
            var inFlightPayment = await _context.Payments
                .Where(p => p.OrderId == order.Id
                    && p.PaymentStatus == PaymentStatus.Initiated
                    && p.Provider == "Zibal")
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (inFlightPayment != null)
            {
                if ((long)inFlightPayment.Amount == amountInTomans &&
                    (DateTime.UtcNow - inFlightPayment.CreatedAt).TotalHours < 6)
                {
                    if (inFlightPayment.Authority.StartsWith(PendingAuthorityPrefix, StringComparison.Ordinal))
                    {
                        TempData["Info"] = "درخواست پرداخت شما در حال آماده‌سازی است. لطفاً چند لحظه دیگر وضعیت سفارش را بررسی کنید و دوباره درخواست نسازید.";
                        return failureRedirect();
                    }
                    return Redirect(ZibalPaymentService.StartPaymentUrl + inFlightPayment.Authority);
                }

                // مبلغ یا عمر پرداخت قبلی معتبر نیست → آن پرداخت باطل و پرداخت تازه ساخته میشود
                var expiredAttempt = await _context.Payments
                    .Where(p => p.Id == inFlightPayment.Id && p.PaymentStatus == PaymentStatus.Initiated)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentStatus, PaymentStatus.Failed));
                if (expiredAttempt != 1)
                {
                    TempData["Info"] = "وضعیت پرداخت هم‌زمان تغییر کرده است؛ سفارش را دوباره بررسی کنید و پرداخت تازه نسازید.";
                    return failureRedirect();
                }
            }

            // ثبت claim در دیتابیس پیش از فراخوانی شبکه؛ ایندکس یکتای filtered در SQL Server
            // تضمین می‌کند دو درخواست هم‌زمان برای یک سفارش هرگز دو نشست درگاه نسازند.
            var attempt = new Payment
            {
                OrderId = order.Id,
                Authority = PendingAuthorityPrefix + Guid.NewGuid().ToString("N"),
                Amount = amountInTomans,
                Provider = "Zibal",
                PaymentStatus = PaymentStatus.Initiated,
                CreatedAt = DateTime.UtcNow,
                TransactionCode = string.Empty
            };
            _context.Payments.Add(attempt);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var active = await _context.Payments.AsNoTracking()
                    .Where(p => p.OrderId == order.Id && p.Provider == "Zibal" && p.PaymentStatus == PaymentStatus.Initiated)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();
                if (active == null) throw;

                _logger.LogInformation(ex, "Concurrent payment-attempt claim rejected for order {OrderId}.", order.Id);
                if (!active.Authority.StartsWith(PendingAuthorityPrefix, StringComparison.Ordinal))
                    return Redirect(ZibalPaymentService.StartPaymentUrl + active.Authority);
                TempData["Info"] = "درخواست پرداخت دیگری برای این سفارش در حال آماده‌سازی است. چند لحظه دیگر دوباره بررسی کنید.";
                return failureRedirect();
            }

            string callbackUrl = Url.Action("Callback", "Payment", new { orderId = order.Id }, Request.Scheme)!;
            string description = isWalletRecharge ? "شارژ کیف پول" : $"پرداخت سفارش {order.OrderCode}";

            var zibalMerchant = await ResolveActiveZibalMerchantAsync();
            (bool Success, string TrackId, string PaymentUrl, string ErrorMessage) result;
            try
            {
                result = await _zibalPaymentService.RequestPayment(amountInRials, description, callbackUrl, merchant: zibalMerchant);
            }
            catch (Exception ex)
            {
                // نتیجهٔ timeout/قطع ارتباط مبهم است؛ claim را باز نگه می‌داریم تا درخواست دوم
                // نشست بانکی تکراری نسازد. رکورد پس از شش ساعت منقضی می‌شود.
                _logger.LogError(ex, "Could not determine the result of Zibal payment-session creation for order {OrderId}.", order.Id);
                TempData["Error"] = "نتیجهٔ اتصال به درگاه مشخص نشد. برای جلوگیری از پرداخت تکراری، چند دقیقه صبر کنید و دوباره بررسی کنید.";
                return failureRedirect();
            }

            if (result.Success && !string.IsNullOrWhiteSpace(result.TrackId) &&
                result.PaymentUrl.StartsWith(ZibalPaymentService.StartPaymentUrl, StringComparison.Ordinal) &&
                result.PaymentUrl.Length > ZibalPaymentService.StartPaymentUrl.Length)
            {
                attempt.Authority = result.TrackId;
                attempt.TransactionCode = result.TrackId;
                await _context.SaveChangesAsync();
                return Redirect(result.PaymentUrl);
            }
            else
            {
                attempt.PaymentStatus = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["Error"] = result.ErrorMessage;
                return failureRedirect();
            }
        }
        /// <summary>
        /// بازگشت از درگاه پرداخت.
        /// ⚠️ عمداً AllowAnonymous است: اگر نشست کاربر بین رفتن به درگاه و بازگشت منقضی شود،
        /// پرداخت انجام‌شده نباید بی‌پاسخ بماند (پول کم شده ولی سفارش پردازش نشده).
        /// اعتبار پرداخت با تأیید سرور-به-سرور خود درگاه (VerifyPayment) و پیگیری رکورد پرداخت
        /// انجام می‌شود، نه با پارامترهای URL؛ پس نبود کوکی، ریسک امنیتی ایجاد نمی‌کند.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Callback(int orderId, string? trackId, string? authority, string? success, string? status)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            // هر درگاه، شناسه تراکنش را با نام خودش برمی‌گرداند: زیبال trackId و زرین‌پال Authority
            var reference = !string.IsNullOrWhiteSpace(trackId) ? trackId
                : !string.IsNullOrWhiteSpace(authority) ? authority
                : null;
            if (string.IsNullOrWhiteSpace(reference)) return NotFound();

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Authority == reference);
            if (payment == null) return NotFound();

            bool isWalletRecharge = order.Notes == OrderNotes.WalletRecharge;

            if (payment.PaymentStatus == PaymentStatus.Refunded)
            {
                TempData["Info"] = "این پرداخت قبلاً مسترد شده است.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            if (payment.PaymentStatus == PaymentStatus.RefundRequired)
            {
                TempData["Error"] = "پرداخت بانکی دریافت شده اما نیازمند بررسی پشتیبانی است. لطفاً دوباره پرداخت نکنید.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            if (payment.PaymentStatus == PaymentStatus.Succeeded)
            {
                TempData["Info"] = "این پرداخت قبلاً پردازش شده است.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: true);
            }

            // ⚠️ در این نسخه فقط زیبال به پرداخت متصل است. اگر رکورد پرداختی با درگاه دیگری وجود داشته باشد،
            // به‌جای تأیید با درگاه نادرست، پردازش متوقف و ثبت خطا میشود.
            if (!string.Equals(payment.Provider, "Zibal", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Payment {PaymentId} uses provider '{Provider}' which is not wired in this build; callback ignored.", payment.Id, payment.Provider);
                TempData["Error"] = "این پرداخت با درگاهی ثبت شده که در این نسخه پشتیبانی نمیشود. لطفاً با پشتیبانی تماس بگیرید.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            // موفقیت اعلامی درگاه زیبال: success=1 و status=2
            bool gatewayReportsSuccess = success == "1" && status == "2";

            if (!gatewayReportsSuccess)
            {
                // A cancellation callback must not overwrite a concurrent successful verification.
                var markedFailed = await _context.Payments
                    .Where(p => p.Id == payment.Id && p.PaymentStatus == PaymentStatus.Initiated)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentStatus, PaymentStatus.Failed));
                if (markedFailed == 0)
                {
                    var currentStatus = await _context.Payments.AsNoTracking()
                        .Where(p => p.Id == payment.Id)
                        .Select(p => p.PaymentStatus)
                        .SingleAsync();
                    if (currentStatus == PaymentStatus.Succeeded)
                    {
                        TempData["Info"] = "این پرداخت قبلاً پردازش شده است.";
                        return await RedirectAfterCallback(order, isWalletRecharge, success: true);
                    }
                    if (currentStatus == PaymentStatus.RefundRequired)
                    {
                        TempData["Error"] = "این پرداخت نیازمند بررسی پشتیبانی است؛ لطفاً دوباره پرداخت نکنید.";
                        return await RedirectAfterCallback(order, isWalletRecharge, success: false);
                    }
                }
                TempData["Error"] = "پرداخت توسط کاربر لغو شد یا ناموفق بود.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            var zibalMerchant = await ResolveActiveZibalMerchantAsync();
            var verifyResult = await _zibalPaymentService.VerifyPayment(reference, zibalMerchant);
            if (!verifyResult.Success)
            {
                // Keep a concurrent success or review state intact when verification calls race.
                var markedFailed = await _context.Payments
                    .Where(p => p.Id == payment.Id && p.PaymentStatus == PaymentStatus.Initiated)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentStatus, PaymentStatus.Failed));
                if (markedFailed == 0)
                {
                    var currentStatus = await _context.Payments.AsNoTracking()
                        .Where(p => p.Id == payment.Id)
                        .Select(p => p.PaymentStatus)
                        .SingleAsync();
                    if (currentStatus == PaymentStatus.Succeeded)
                    {
                        TempData["Info"] = "این پرداخت قبلاً پردازش شده است.";
                        return await RedirectAfterCallback(order, isWalletRecharge, success: true);
                    }
                    if (currentStatus == PaymentStatus.RefundRequired)
                    {
                        TempData["Error"] = "این پرداخت نیازمند بررسی پشتیبانی است؛ لطفاً دوباره پرداخت نکنید.";
                        return await RedirectAfterCallback(order, isWalletRecharge, success: false);
                    }
                }
                TempData["Error"] = verifyResult.ErrorMessage;
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            payment.TransactionCode = verifyResult.RefNumber.ToString();

            // A captured callback from another attempt must not clear an already open review.
            // Record each additional capture for reconciliation while keeping the order blocked.
            var anotherCapturedPaymentNeedsReview = await _context.Payments.AnyAsync(p =>
                p.OrderId == order.Id && p.Id != payment.Id && p.PaymentStatus == PaymentStatus.RefundRequired);
            if (order.PaymentStatus == PaymentStatus.RefundRequired
                || order.OrderStatus == OrderStatus.PaymentReview
                || anotherCapturedPaymentNeedsReview)
            {
                payment.PaymentStatus = PaymentStatus.RefundRequired;
                order.OrderStatus = OrderStatus.PaymentReview;
                order.PaymentStatus = PaymentStatus.RefundRequired;
                order.IsPaymentEnabled = false;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogCritical("Verified payment {PaymentId} for order {OrderId} arrived while another payment review was unresolved.", payment.Id, order.Id);
                TempData["Error"] = "پرداخت بانکی دریافت شده اما این سفارش پرداخت نیازمند بررسی دیگری دارد. پرداخت دوباره انجام ندهید؛ پشتیبانی همه تراکنش‌ها را بررسی می‌کند.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            // پرداختی که با تلاش جدیدتری جایگزین شده، هرگز نباید دوباره به سفارش اعمال شود.
            // اگر درگاه آن را با وجود supersede شدن دریافت کرده باشد، ثبت برای استرداد/رسیدگی لازم است.
            var latestPaymentId = await _context.Payments
                .Where(p => p.OrderId == order.Id && p.Provider == "Zibal")
                .MaxAsync(p => (int?)p.Id) ?? payment.Id;
            if (payment.Id != latestPaymentId)
            {
                payment.PaymentStatus = PaymentStatus.RefundRequired;
                order.OrderStatus = OrderStatus.PaymentReview;
                order.PaymentStatus = PaymentStatus.RefundRequired;
                order.IsPaymentEnabled = false;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogCritical("Verified payment {PaymentId} for order {OrderId} belongs to a superseded attempt and requires reconciliation.", payment.Id, order.Id);
                TempData["Error"] = "پرداخت بانکی دریافت شده اما این نشست پرداخت قدیمی بوده است. پرداخت دوباره انجام ندهید؛ پشتیبانی سفارش را بررسی می‌کند.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            // ── بررسی مغایرت مبلغ (پرداخت مرحله‌ای): مبلغ این مرحله = کل نهایی‌شده − پرداخت‌های قبلی ──
            // اگر قیمت سفارش در فاصله پرداخت توسط فروشگاه تغییر کرده باشد، پرداخت پذیرفته نمی‌شود.
            if (!isWalletRecharge)
            {
                var pricingBeforeThisPayment = await GetPricingAsync(order);
                var stageAmount = (long)Math.Ceiling(pricingBeforeThisPayment.PayableNow);
                if (payment.Amount != stageAmount)
                {
                    _logger.LogError("Amount mismatch for order {OrderId}. Expected: {Expected}, Payment: {Actual}", order.Id, stageAmount, payment.Amount);
                    payment.PaymentStatus = PaymentStatus.RefundRequired;
                    order.OrderStatus = OrderStatus.PaymentReview;
                    order.PaymentStatus = PaymentStatus.RefundRequired;
                    order.IsPaymentEnabled = false;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogCritical("Verified payment {PaymentId} for order {OrderId} has an amount mismatch and requires reconciliation.", payment.Id, order.Id);
                    TempData["Error"] = "پرداخت بانکی دریافت شده اما مبلغ با سفارش مغایرت دارد. لطفاً دوباره پرداخت نکنید؛ پشتیبانی پیگیری می‌کند.";
                    return await RedirectAfterCallback(order, isWalletRecharge, success: false);
                }
            }

            // ===== پردازش نهایی اتمیک و idempotent =====
            // وضعیت پرداخت فقط به‌صورت شرطی از Initiated به Succeeded تغییر می‌کند؛
            // اگر دو درخواست همزمان برسند، فقط یکی موفق می‌شود (جلوگیری از شارژ/کسر مضاعف).
            // کسر موجودی انبار و شارژ کیف پول هم در همان تراکنش انجام می‌شود تا ناسازگاری ایجاد نشود.
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                // هر دو DbContext باید روی یک اتصال مشترک کار کنند تا در یک تراکنش واحد شرکت کنند
                // (این اتصال مشترک در Program.cs ثبت شده است). اگر به‌هر دلیل اتصال‌ها متفاوت شوند،
                // UseTransaction خطا می‌دهد؛ این بررسی خطا را به مسیر دوستانه هدایت می‌کند نه خطای 500.
                if (!ReferenceEquals(_catalogDb.Database.GetDbConnection(), _context.Database.GetDbConnection()))
                    throw new InvalidOperationException("Sales and Catalog DbContexts are not sharing a connection; atomic payment processing aborted.");

                _catalogDb.Database.UseTransaction(tx.GetDbTransaction());

                // Serialize verified callbacks for this order, then re-read its state after
                // waiting for any earlier callback's transaction to finish.
                var callbackLockTime = DateTime.UtcNow;
                var lockedOrder = await _context.Orders
                    .Where(o => o.Id == order.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.UpdatedAt, callbackLockTime));
                if (lockedOrder != 1)
                {
                    await tx.RollbackAsync();
                    return NotFound();
                }
                await _context.Entry(order).ReloadAsync();

                var currentAttemptStatus = await _context.Payments.AsNoTracking()
                    .Where(p => p.Id == payment.Id)
                    .Select(p => p.PaymentStatus)
                    .SingleAsync();
                if (currentAttemptStatus == PaymentStatus.Succeeded)
                {
                    await tx.RollbackAsync();
                    TempData["Info"] = "این پرداخت قبلاً پردازش شده است.";
                    return await RedirectAfterCallback(order, isWalletRecharge, success: true);
                }
                if (currentAttemptStatus == PaymentStatus.RefundRequired)
                {
                    await tx.RollbackAsync();
                    TempData["Error"] = "این پرداخت قبلاً برای بررسی ثبت شده است؛ لطفاً دوباره پرداخت نکنید.";
                    return await RedirectAfterCallback(order, isWalletRecharge, success: false);
                }

                string? reviewReason = null;
                var anotherPaymentNeedsReview = await _context.Payments.AnyAsync(p =>
                    p.OrderId == order.Id && p.Id != payment.Id && p.PaymentStatus == PaymentStatus.RefundRequired);
                if (order.PaymentStatus == PaymentStatus.Refunded || order.OrderStatus == OrderStatus.Cancelled)
                {
                    reviewReason = "the order was closed or refunded before this payment was verified";
                }
                else if (order.PaymentStatus == PaymentStatus.RefundRequired
                    || order.OrderStatus == OrderStatus.PaymentReview
                    || anotherPaymentNeedsReview)
                {
                    reviewReason = "an earlier payment review is unresolved";
                }
                else
                {
                    var currentLatestPaymentId = await _context.Payments
                        .Where(p => p.OrderId == order.Id && p.Provider == "Zibal")
                        .MaxAsync(p => (int?)p.Id) ?? payment.Id;
                    if (payment.Id != currentLatestPaymentId)
                        reviewReason = "the verified payment attempt was superseded";
                    else if (currentAttemptStatus == PaymentStatus.Failed)
                        reviewReason = "the verified payment attempt was already marked failed";
                    else if (!isWalletRecharge)
                    {
                        var currentPricing = await GetPricingAsync(order);
                        if (payment.Amount != (long)Math.Ceiling(currentPricing.PayableNow))
                            reviewReason = "the order amount changed during payment verification";
                    }
                }

                if (reviewReason != null)
                {
                    payment.PaymentStatus = PaymentStatus.RefundRequired;
                    order.OrderStatus = OrderStatus.PaymentReview;
                    order.PaymentStatus = PaymentStatus.RefundRequired;
                    order.IsPaymentEnabled = false;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    _logger.LogCritical("Verified payment {PaymentId} for order {OrderId} requires review: {Reason}.", payment.Id, order.Id, reviewReason);
                    TempData["Error"] = "پرداخت بانکی دریافت شده اما سفارش نیازمند بررسی است. پرداخت دوباره انجام ندهید؛ پشتیبانی تراکنش را بررسی می‌کند.";
                    return await RedirectAfterCallback(order, isWalletRecharge, success: false);
                }

                // Only a still-active attempt can be applied. A late success for a failed or
                // superseded attempt is retained for manual reconciliation instead.
                var claimed = await _context.Payments
                    .Where(p => p.Id == payment.Id && p.PaymentStatus == PaymentStatus.Initiated)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.PaymentStatus, PaymentStatus.Succeeded)
                        .SetProperty(p => p.TransactionCode, verifyResult.RefNumber.ToString()));

                if (claimed == 0)
                {
                    var statusAfterClaim = await _context.Payments.AsNoTracking()
                        .Where(p => p.Id == payment.Id)
                        .Select(p => p.PaymentStatus)
                        .SingleAsync();
                    if (statusAfterClaim == PaymentStatus.Failed)
                    {
                        payment.PaymentStatus = PaymentStatus.RefundRequired;
                        order.OrderStatus = OrderStatus.PaymentReview;
                        order.PaymentStatus = PaymentStatus.RefundRequired;
                        order.IsPaymentEnabled = false;
                        order.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                        _logger.LogCritical("Verified payment {PaymentId} for order {OrderId} changed state while being verified; it requires reconciliation.", payment.Id, order.Id);
                        TempData["Error"] = "پرداخت بانکی دریافت شده اما وضعیت نشست پرداخت هم‌زمان تغییر کرده است. پرداخت دوباره انجام ندهید؛ پشتیبانی پیگیری می‌کند.";
                        return await RedirectAfterCallback(order, isWalletRecharge, success: false);
                    }

                    await tx.RollbackAsync();
                    TempData["Info"] = "این پرداخت قبلاً پردازش شده است.";
                    return await RedirectAfterCallback(order, isWalletRecharge,
                        success: statusAfterClaim == PaymentStatus.Succeeded);
                }

                // ثبت اتمیک claim سفارش و کسر شرطی همه اقلام. اگر فقط یکی از کالاها کمبود داشته
                // باشد، savepoint کل کسرهای جزئی را rollback می‌کند ولی پرداخت دریافت‌شده برای پیگیری ثبت می‌شود.
                if (!isWalletRecharge && order.Items.Any() && order.InventoryDeductedAt == null)
                {
                    await tx.CreateSavepointAsync("BeforeInventoryClaim");
                    var reservationTime = DateTime.UtcNow;
                    var orderClaimed = await _context.Orders
                        .Where(o => o.Id == order.Id && o.InventoryDeductedAt == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(o => o.InventoryDeductedAt, reservationTime));

                    var inventoryAvailable = orderClaimed == 1
                        && await _inventoryService.TryDecreaseInventoryAsync(order.Items);
                    if (!inventoryAvailable)
                    {
                        await tx.RollbackToSavepointAsync("BeforeInventoryClaim");
                        payment.PaymentStatus = PaymentStatus.RefundRequired;
                        order.OrderStatus = OrderStatus.PaymentReview;
                        order.PaymentStatus = PaymentStatus.RefundRequired;
                        order.IsPaymentEnabled = false;
                        order.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                        _logger.LogCritical("Payment {PaymentId} for order {OrderId} was verified but inventory was unavailable; order requires manual fulfillment or refund.", payment.Id, order.Id);
                        TempData["Error"] = "پرداخت بانکی دریافت شده اما موجودی سفارش کافی نیست. لطفاً دوباره پرداخت نکنید؛ پشتیبانی برای تأمین کالا یا استرداد پیگیری می‌کند.";
                        return await RedirectAfterCallback(order, isWalletRecharge, success: false);
                    }

                    order.InventoryDeductedAt = reservationTime;
                }

                if (isWalletRecharge)
                {
                    // شارژ کیف پول: تک‌مرحله‌ای است و مستقیماً کامل محسوب می‌شود
                    order.PaymentStatus = PaymentStatus.Succeeded;
                    order.OrderStatus = OrderStatus.Paid;
                    order.IsPaymentEnabled = false;
                    await _context.SaveChangesAsync();
                }
                else if (!order.Items.Any())
                {
                    // سفارش کیک سفارشی (tempOrder بدون ردیف): پرداخت تک‌مرحله‌ای است
                    order.PaymentStatus = PaymentStatus.Succeeded;
                    order.OrderStatus = OrderStatus.Paid;
                    order.IsPaymentEnabled = false;
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // ── وضعیت سفارش در پرداخت مرحله‌ای ──
                    // تا وقتی جعبه وزن‌کشی‌نشده مانده یا مبلغی باقیمانده، سفارش «نیمه‌پرداخت» است:
                    // PaymentStatus = Unpaid می‌ماند (تا مرحله بعد قابل پرداخت باشد) و IsPaymentEnabled فعال می‌ماند.
                    // فقط وقتی کل مبلغ نهایی‌شده پوشش داده شد، سفارش «Paid» کامل می‌شود.
                    var pricingNow = await GetPricingAsync(order);
                    bool allBoxesFinalized = !pricingNow.HasUnfinalizedBoxes;
                    bool fullyPaid = allBoxesFinalized
                        && pricingNow.GrandTotal > 0
                        && pricingNow.PaidTotal >= pricingNow.GrandTotal;
                    order.PaymentStatus = fullyPaid ? PaymentStatus.Succeeded : PaymentStatus.Unpaid;
                    order.IsPaymentEnabled = !fullyPaid;      // مرحله بعد فعال بماند؛ پس از تسویه کامل خاموش
                    order.OrderStatus = fullyPaid ? OrderStatus.Paid : OrderStatus.AwaitingReview;
                    await _context.SaveChangesAsync();
                }

                // ── تسویه سفارش کیک سفارشی ──
                // 🔒 نشانه CustomCakeOrder_ از فیلد Notes می‌آید؛ پس فقط سفارش موقتِ بدون ردیف کالا
                // که متعلق به همان کاربر است می‌تواند سفارش کیک را پرداخت‌شده کند.
                var cakeOrderId = OrderNotes.TryParseCustomCakeOrderId(order.Notes);
                if (cakeOrderId.HasValue)
                {
                    if (!order.Items.Any())
                    {
                        var cakeOrder = await _context.CustomCakeOrders.FindAsync(cakeOrderId.Value);
                        bool isValidCakePayment = cakeOrder != null
                            && !string.IsNullOrEmpty(order.UserId)
                            && cakeOrder.UserId == order.UserId
                            && cakeOrder.Status == CustomCakeOrderStatus.Accepted
                            && !cakeOrder.IsPaid;

                        if (isValidCakePayment)
                        {
                            cakeOrder!.IsPaid = true;
                            cakeOrder.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            _logger.LogWarning("Order {OrderId} referenced custom cake {CakeOrderId} that is not a payable order of user {UserId}; cake left unpaid.",
                                order.Id, cakeOrderId.Value, order.UserId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Order {OrderId} of user {UserId} carried a reserved CustomCakeOrder_ marker; ignoring it.",
                            order.Id, order.UserId);
                    }
                }

                if (isWalletRecharge)
                {
                    var wallet = await WalletBalanceLock.GetOrCreateForUpdateAsync(_context, order.UserId!);
                    wallet.Balance += order.TotalAmountSnapshot;
                    wallet.UpdatedAt = DateTime.UtcNow;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = order.UserId!,
                        Amount = order.TotalAmountSnapshot,
                        Type = "DirectRecharge",
                        Description = "شارژ مستقیم کیف پول از طریق درگاه زیبال",
                        OrderId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // ── پیامک تأیید پرداخت + لینک صورت‌حساب به مشتری (خارج از تراکنش بانکی، از طریق صف) ──
                if (!isWalletRecharge)
                {
                    // آدرس پایه از مسیر همین درخواست ساخته می‌شود تا دامنه‌ی لینک پیامک همان دامنه‌ای
                    // باشد که مشتری همین الان در آن است (آدرس تنظیم‌شده در پنل، اگر باشد، مقدم است).
                    var requestBaseUrl = $"{Request.Scheme}://{Request.Host}";

                    try { await _sms.NotifyPaymentConfirmedAsync(order, requestBaseUrl); }
                    catch (Exception smsEx) { _logger.LogWarning(smsEx, "SMS payment-confirmed notify failed for order {OrderId}", order.Id); }

                    // لینک صورت‌حساب فقط برای سفارش‌هایی که واقعاً سند دارند ساخته می‌شود
                    // (سفارش‌های داخلی کیک سفارشی ردیف کالا ندارند و سندشان صادر نمی‌شود).
                    if (cakeOrderId == null && order.Items.Any())
                    {
                        try { await _sms.NotifyPaymentStatementAsync(order, requestBaseUrl); }
                        catch (Exception smsEx) { _logger.LogWarning(smsEx, "SMS payment-statement notify failed for order {OrderId}", order.Id); }
                    }
                }
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در پردازش نهایی پرداخت سفارش {OrderId}؛ تراکنش برگشت داده شد", order.Id);
                TempData["Error"] = "خطا در ثبت پرداخت. اگر مبلغی کسر شده است، با پشتیبانی تماس بگیرید.";
                return await RedirectAfterCallback(order, isWalletRecharge, success: false);
            }

            TempData["Success"] = isWalletRecharge
                ? $"کیف پول شما با موفقیت شارژ شد. شماره پیگیری: {verifyResult.RefNumber}"
                : $"پرداخت با موفقیت انجام شد. شماره پیگیری: {verifyResult.RefNumber}";
            return await RedirectAfterCallback(order, isWalletRecharge, success: true);
        }

        /// <summary>
        /// خواندن کلید پذیرنده زیبال از تنظیمات دیتابیس (حساب فعال درگاه زیبال).
        /// اگر هنوز کلیدی ذخیره نشده باشد، مقدار appsettings (پیش‌فرض تست) استفاده می‌شود.
        /// </summary>
        private async Task<string?> ResolveActiveZibalMerchantAsync()
        {
            var merchant = _configuration["Zibal:Merchant"];

            try
            {
                var gateway = await _context.PaymentGateways
                    .Include(g => g.Accounts)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.GatewayType == "Zibal" && g.IsActive);

                var account = gateway?.Accounts?.FirstOrDefault(a => a.IsActive && !string.IsNullOrWhiteSpace(a.ConfigData));
                if (account != null)
                {
                    using var doc = JsonDocument.Parse(account.ConfigData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("MerchantId", out var mid) && mid.ValueKind == JsonValueKind.String)
                    {
                        var stored = mid.GetString();
                        if (!string.IsNullOrWhiteSpace(stored))
                            merchant = stored.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "عدم موفقیت در خواندن تنظیمات درگاه زیبال از دیتابیس؛ استفاده از مقدار پیش‌فرض");
            }

            return merchant;
        }
    }
}
