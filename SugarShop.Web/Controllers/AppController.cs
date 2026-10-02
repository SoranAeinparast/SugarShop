using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace SugarShop.Web.Controllers
{
    /// <summary>
    /// صفحه‌ی دانلود اپلیکیشن اندروید — با QR کد آماده برای چاپ و اشتراک‌گذاری.
    /// نام فروشگاه و لوگو از تنظیمات خوانده می‌شود تا صفحه همیشه با برند فعلی هماهنگ باشد.
    /// دانلود APK از مسیر /App/GetTheApp عبور می‌کند تا هر دانلود در دیتابیس ثبت شود
    /// و داشبورد مدیریت آمار دقیق داشته باشد.
    /// </summary>
    public class AppController : Controller
    {
        private readonly SugarShop.Infrastructure.Persistence.Sales.SugarShopSalesDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AppController> _logger;

        /// <summary>نسخه‌ی فعلی APK — با AndroidManifest (versionName) هماهنگ نگه داشته شود.</summary>
        public const string ApkVersion = "1.7.1";

        public AppController(SugarShop.Infrastructure.Persistence.Sales.SugarShopSalesDbContext db,
            IWebHostEnvironment env, ILogger<AppController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        [Route("App/Download")]
        public async Task<IActionResult> Download()
        {
            var theme = await _db.ThemeSettings.AsNoTracking().OrderByDescending(t => t.Id).FirstOrDefaultAsync();
            if (theme != null && !theme.AppEnabled)
                return NotFound(); // ارائه‌ی اپلیکیشن از تنظیمات ظاهری غیرفعال شده

            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var storeName = string.IsNullOrWhiteSpace(settings?.SiteTitle)
                ? "شیرینی سرا"
                : settings.SiteTitle!;
            ViewBag.StoreName = string.IsNullOrWhiteSpace(theme?.AppDisplayName) ? storeName : theme.AppDisplayName!;
            ViewBag.LogoPath = string.IsNullOrWhiteSpace(settings?.LogoPath)
                ? "/Pwa/Icon?size=192"
                : settings.LogoPath;
            ViewBag.ApkVersion = ApkVersion;
            ViewBag.AndroidEnabled = theme?.AppAndroidEnabled ?? true;
            ViewBag.IosEnabled = theme?.AppIosEnabled ?? true;
            // آدرس پایه‌ی اپ: تنظیم ادمین، وگرنه دامنه‌ی فعلی — تغییر دامنه هرگز صفحه را نمی‌شکند
            ViewBag.AppBaseUrl = string.IsNullOrWhiteSpace(theme?.AppBaseUrl) ? "" : theme.AppBaseUrl!;
            return View();
        }

        /// <summary>
        /// QR کد صفحه‌ی دانلود — به‌صورت پویا با QRCoder ساخته می‌شود و همیشه به
        /// «آدرس پایه‌ی فعال» (تنظیم ادمین یا دامنه‌ی جاری) اشاره می‌کند؛
        /// پس تغییر دامنه نیازی به بازتولید هیچ فایلی ندارد.
        /// </summary>
        [HttpGet]
        [Route("App/Qr")]
        public async Task<IActionResult> Qr()
        {
            var theme = await _db.ThemeSettings.AsNoTracking().OrderByDescending(t => t.Id).FirstOrDefaultAsync();
            if (theme != null && !theme.AppEnabled)
                return NotFound();

            var baseUrl = string.IsNullOrWhiteSpace(theme?.AppBaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : theme.AppBaseUrl!;
            var target = baseUrl.TrimEnd('/') + "/App/Download";

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(target, QRCodeGenerator.ECCLevel.Q);
            var svg = new SvgQRCode(data).GetGraphic(6, "#4E342E", "#FFF8F0");
            Response.Headers["Cache-Control"] = "public, max-age=600";
            return Content(svg, "image/svg+xml");
        }

        /// <summary>
        /// دانلود ردیابی‌شده‌ی APK — هر درخواست با IP و مرورگر ثبت می‌شود.
        /// </summary>
        [HttpGet]
        [Route("App/GetTheApp")]
        public async Task<IActionResult> GetTheApp()
        {
            // مسیر فایل: کنار ریشه‌ی محتوا (publish) یا کنار پروژه‌ی وب (dev)
            var apkPath = Path.Combine(_env.ContentRootPath, "Resources", "pastry-app.apk");
            if (!System.IO.File.Exists(apkPath))
                apkPath = Path.Combine(_env.ContentRootPath, "SugarShop.Web", "Resources", "pastry-app.apk");
            if (!System.IO.File.Exists(apkPath))
            {
                _logger.LogError("APK file missing at {Path}", apkPath);
                return NotFound("فایل اپلیکیشن یافت نشد. لطفاً بعداً تلاش کنید.");
            }

            try
            {
                _db.AppDownloadLogs.Add(new SugarShop.Domain.Entities.AppDownloadLog
                {
                    FilePath = "/App/GetTheApp",
                    AppVersion = ApkVersion,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Truncate(Request.Headers.UserAgent.ToString()),
                    Referrer = Truncate(Request.Headers.Referer.ToString()),
                    DownloadedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // ثبت لاگ هرگز نباید مانع دانلود شود
                _logger.LogWarning(ex, "Failed to record app download log");
            }

            return PhysicalFile(apkPath, "application/vnd.android.package-archive",
                "pastry-app.apk", enableRangeProcessing: false);
        }

        private static string? Truncate(string? value, int max = 400)
            => string.IsNullOrEmpty(value) ? null : value[..Math.Min(value.Length, max)];
    }
}
