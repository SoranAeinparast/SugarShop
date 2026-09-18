using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.ViewComponents
{
    /// <summary>
    /// اکشن «باز کردن در اپلیکیشن» برای صفحه‌هایی که مقصد لینک پیامکی‌اند.
    ///
    /// مسیر اصلیِ باز شدن لینک در اپ، Android App Links است: لینک https + فایل
    /// <c>/.well-known/assetlinks.json</c> + اثر انگشت امضا؛ اندروید خودش لینک را در اپ باز می‌کند.
    /// این کامپوننت فقط پشتیبان است برای حالت‌هایی که آن مسیر کار نمی‌کند — مرورگرهایی که App Links
    /// را پشتیبانی نمی‌کنند (مثل مرورگر داخلی تلگرام) یا گوشی‌هایی که تأیید مالکیت روی‌شان انجام نشده.
    /// با دادن نام پکیج به‌صورت صریح، اپ بدون نیاز به تأیید باز می‌شود.
    /// </summary>
    public class AppLinkBarViewComponent : ViewComponent
    {
        private readonly IConfiguration _config;
        private readonly SugarShopSalesDbContext _context;

        public AppLinkBarViewComponent(IConfiguration config, SugarShopSalesDbContext context)
        {
            _config = config;
            _context = context;
        }

        /// <param name="orderId">اگر بدهید، داخل اپ لینک «بازگشت به سفارش من» هم نمایش داده می‌شود.</param>
        public async Task<IViewComponentResult> InvokeAsync(int? orderId = null)
        {
            var packageName = _config["Twa:PackageName"];
            if (string.IsNullOrWhiteSpace(packageName)) return Content(string.Empty);

            // اگر اپ در تنظیمات ظاهری غیرفعال شده باشد، دکمه‌ای نشان داده نمی‌شود
            var appEnabled = (await _context.ThemeSettings.AsNoTracking()
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync())?.AppEnabled ?? true;
            if (!appEnabled) return Content(string.Empty);

            var request = ViewContext.HttpContext.Request;
            var hostAndPath = $"{request.Host}{request.Path}{request.QueryString}";
            var currentUrl = $"{request.Scheme}://{hostAndPath}";

            // اگر اپ نصب نباشد، مرورگر همین صفحه را نگه می‌دارد (fallback)
            var intentUrl = $"intent://{hostAndPath}#Intent;scheme={request.Scheme};package={packageName.Trim()};"
                          + $"S.browser_fallback_url={Uri.EscapeDataString(currentUrl)};end";

            return View(new AppLinkBarModel
            {
                PackageName = packageName.Trim(),
                IntentUrl = intentUrl,
                OrderId = orderId
            });
        }
    }

    public class AppLinkBarModel
    {
        public string PackageName { get; set; } = string.Empty;
        public string IntentUrl { get; set; } = string.Empty;
        public int? OrderId { get; set; }
    }
}
