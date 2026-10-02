using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Versioning;

namespace SugarShop.Web.Controllers
{
    /// <summary>
    /// Endpoints جانبی PWA: مانیفست پویا، آیکون پویا و صفحه آفلاین.
    /// آیکون اپ همیشه از «لوگوی فعلی فروشگاه» ساخته می‌شود — اگر ادمین لوگو را عوض کند،
    /// آیکون شورت‌کات‌های جدید به‌صورت خودکار از لوگوی جدید ساخته می‌شود.
    /// </summary>
    public class PwaController : Controller
    {
        private readonly SugarShop.Infrastructure.Persistence.Sales.SugarShopSalesDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PwaController> _logger;
        private readonly IConfiguration _config;

        public PwaController(SugarShop.Infrastructure.Persistence.Sales.SugarShopSalesDbContext db,
            IWebHostEnvironment env, ILogger<PwaController> logger, IConfiguration config)
        {
            _db = db;
            _env = env;
            _logger = logger;
            _config = config;
        }

        [HttpGet]
        [Route("manifest.webmanifest")]
        public async Task<IActionResult> Manifest()
        {
            var theme = await _db.ThemeSettings.AsNoTracking().OrderByDescending(t => t.Id).FirstOrDefaultAsync();
            if (theme != null && !theme.AppEnabled)
            {
                Response.StatusCode = 404;
                return Json(new { error = "app disabled" });
            }

            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var name = string.IsNullOrWhiteSpace(theme?.AppDisplayName)
                ? (string.IsNullOrWhiteSpace(settings?.SiteTitle) ? "شیرینی سرا" : settings.SiteTitle!)
                : theme.AppDisplayName!;
            var shortName = name.Length > 12 ? name.Substring(0, 12) : name;

            // v بر اساس آخرین تغییر لوگو — تا وقتی لوگو عوض شد، آیکون‌های کش‌شده باطل شوند
            var logoV = await LogoVersionAsync();

            var manifest = new
            {
                // هویت یکتای اپ — از دامنه‌ی فعال ساخته می‌شود تا با تغییر دامنه، هویت اپ هم خودکار تازه شود
                id = ManifestId(Request.Host.Host),
                name,
                short_name = shortName,
                description = settings?.SiteDescription ?? "فروشگاه شیرینی و کیک",
                start_url = "/",
                scope = "/",
                display = "standalone",
                orientation = "portrait",
                dir = "rtl",
                lang = "fa",
                background_color = "#FFF8F0",
                theme_color = "#8B4513",
                icons = new object[]
                {
                    new { src = $"/Pwa/Icon?size=192&v={logoV}", sizes = "192x192", type = "image/png", purpose = "any" },
                    new { src = $"/Pwa/Icon?size=512&v={logoV}", sizes = "512x512", type = "image/png", purpose = "any" },
                    new { src = $"/Pwa/Icon?size=512&maskable=true&v={logoV}", sizes = "512x512", type = "image/png", purpose = "maskable" }
                },
                shortcuts = new object[]
                {
                    new { name = "کیک سفارشی", url = "/CustomCakeOrder/Create", icons = new[] { new { src = $"/Pwa/Icon?size=192&v={logoV}", sizes = "192x192" } } },
                    new { name = "سفارش‌های من", url = "/Profile/Orders", icons = new[] { new { src = $"/Pwa/Icon?size=192&v={logoV}", sizes = "192x192" } } }
                }
            };

            // مانیفست نباید کش بلندمدت شود — تغییر تنظیمات باید سریع دیده شود
            Response.Headers["Cache-Control"] = "public, max-age=3600";
            return Json(manifest);
        }

        /// <summary>
        /// آیکون اپ به‌صورت پویا از لوگوی فروشگاه ساخته می‌شود.
        /// نمونه: /Pwa/Icon?size=192 یا /Pwa/Icon?size=512&amp;maskable=true
        /// </summary>
        [HttpGet]
        [Route("Pwa/Icon")]
        [SupportedOSPlatform("windows")]
        public async Task<IActionResult> Icon([FromQuery] int size = 192, [FromQuery] bool maskable = false)
        {
            size = size switch { <= 192 => 192, <= 512 => 512, _ => 512 };

            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var logoVirtual = string.IsNullOrWhiteSpace(settings?.LogoPath)
                ? "/images/SORANSOFT_LOGO.png"
                : settings.LogoPath!;

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
            {
                _logger.LogWarning("Pwa/Icon: WebRootPath is null");
                return NotFound();
            }
            var logoPhysical = Path.Combine(webRoot, logoVirtual.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(logoPhysical))
                logoPhysical = Path.Combine(webRoot, "images", "SORANSOFT_LOGO.png");

            // کش روی دیسک: کلید = مسیر لوگو + زمان آخرین تغییر آن
            var cacheDir = Path.Combine(webRoot, "icons", "cache");
            Directory.CreateDirectory(cacheDir);
            var logoStamp = System.IO.File.Exists(logoPhysical)
                ? System.IO.File.GetLastWriteTimeUtc(logoPhysical).Ticks.ToString("x")
                : "none";
            var cacheFile = Path.Combine(cacheDir, $"app-{size}{(maskable ? "-mask" : "")}-{logoStamp}.png");

            if (!System.IO.File.Exists(cacheFile))
            {
                try
                {
                    GenerateIconPng(logoPhysical, cacheFile, size, maskable);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PWA icon generation failed for {Logo}", logoPhysical);
                    return NotFound();
                }
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(cacheFile);
            Response.Headers["Cache-Control"] = "public, max-age=604800"; // یک هفته
            return File(bytes, "image/png");
        }

        [HttpGet]
        [Route("Pwa/Offline")]
        public IActionResult Offline()
        {
            Response.Headers["Cache-Control"] = "no-cache";
            return View();
        }

        /// <summary>
        /// تأیید مالکیت اپ اندروید (TWA) — Android هنگام اجرای APK این آدرس را می‌خواند؛
        /// اگر امضای APK با اثر انگشت (fingerprint) اینجا مطابقت کند، اپ تمام‌صفحه و بدون نوار آدرس اجرا می‌شود.
        /// مقادیر از appsettings (بخش Twa) خوانده می‌شوند؛ پس از اولین build، اثر انگشت را در appsettings بگذارید.
        /// تا قبل از تنظیم، آرایهٔ خالی برمی‌گردد (معتبر ولی بدون تأیید — امن).
        /// </summary>
        [HttpGet]
        [Route(".well-known/assetlinks.json")]
        public IActionResult AssetLinks()
        {
            var packageName = _config["Twa:PackageName"];
            var fingerprints = _config.GetSection("Twa:Sha256Fingerprints").Get<string[]>() ?? Array.Empty<string>();

            // تا وقتی PackageName و اثر انگشت تنظیم نشده، خالی می‌دهیم (Android تأیید نمی‌کند ولی خطا هم نمی‌گیرد)
            if (string.IsNullOrWhiteSpace(packageName) || fingerprints.Length == 0)
            {
                Response.Headers["Cache-Control"] = "no-cache";
                return Json(Array.Empty<object>());
            }

            var statements = fingerprints.Select(fp => new
            {
                relation = new[] { "delegate_permission/common.handle_all_urls" },
                target = new
                {
                    @namespace = "android_app",
                    package_name = packageName,
                    sha256_cert_fingerprints = new[] { fp.ToUpperInvariant().Replace(":", "") }
                }
            });

            // تغییرش به‌ندرت می‌خورد اما مهم است زنده باشد — کش کوتاه
            Response.Headers["Cache-Control"] = "public, max-age=3600";
            return Json(statements);
        }

        /// <summary>هویت مانیفست از دامنه ساخته می‌شود: example.com → com.example.app</summary>
        private static string ManifestId(string host)
        {
            var clean = host.Split(':')[0].ToLowerInvariant();
            var parts = clean.Split('.');
            Array.Reverse(parts);
            return string.Join('.', parts) + ".app";
        }

        private async Task<string> LogoVersionAsync()
        {
            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            var logoVirtual = string.IsNullOrWhiteSpace(settings?.LogoPath) ? "" : settings.LogoPath!;
            string stamp = "none";
            var webRoot = _env.WebRootPath;
            if (!string.IsNullOrEmpty(webRoot) && !string.IsNullOrEmpty(logoVirtual))
            {
                var physical = Path.Combine(webRoot, logoVirtual.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                stamp = System.IO.File.Exists(physical)
                    ? System.IO.File.GetLastWriteTimeUtc(physical).Ticks.ToString("x")
                    : "none";
            }
            // هش کوتاه تا URL تمیز بماند
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(logoVirtual + stamp)));
            return hash[..10].ToLowerInvariant();
        }

        [SupportedOSPlatform("windows")]
        private static void GenerateIconPng(string logoPath, string outputPath, int size, bool maskable)
        {
            using var src = System.Drawing.Image.FromFile(logoPath);
            using var bmp = new System.Drawing.Bitmap(size, size);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // پس‌زمینه کِرِم — کامل تا لبه (شرط maskable)
            g.Clear(System.Drawing.Color.FromArgb(255, 255, 248, 240));

            // ناحیه امن maskable: محتوا داخل ۶۲٪ مرکزی؛ عادی ۷۸٪
            var contentBox = maskable ? 0.62 : 0.78;
            var maxW = (int)(size * contentBox);
            var maxH = (int)(size * contentBox);
            var scale = Math.Min(maxW / (double)src.Width, maxH / (double)src.Height);
            var w = (int)(src.Width * scale);
            var h = (int)(src.Height * scale);
            g.DrawImage(src, (size - w) / 2, (size - h) / 2, w, h);

            bmp.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
