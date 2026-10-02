using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Areas.Admin.ViewModels;
using System.Text.Json;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class SiteSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public SiteSettingsController(SugarShopSalesDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SiteSetting();
                _context.SiteSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            // استخراج لینک‌های فعلی برای تیک زدن پیش‌فرض در View
            var currentQuickLinks = new List<string>();
            if (!string.IsNullOrEmpty(settings.QuickLinksJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(settings.QuickLinksJson);
                    currentQuickLinks = parsed?.Select(x => x["Url"]).ToList() ?? new List<string>();
                }
                catch { /* اگر JSON خراب بود، لیست خالی برمی‌گرداند */ }
            }

            ViewBag.AvailableLinks = GetAvailablePages();
            ViewBag.CurrentQuickLinks = currentQuickLinks;

            // ✅ ساخت لیست بصری گواهینامه‌ها از روی JSON (ادمین هرگز با JSON سروکار ندارد)
            ViewBag.Certifications = ParseCertifications(settings.CertificationsJson);

            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SiteSetting model, string[] selectedQuickLinks)
        {
            if (ModelState.IsValid)
            {
                var settings = await _context.SiteSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new SiteSetting();
                    _context.SiteSettings.Add(settings);
                }

                // ... (سایر تنظیمات قبلی بدون تغییر باقی می‌مانند) ...
                settings.LogoPath = model.LogoPath;
                settings.FaviconPath = model.FaviconPath;
                settings.SiteTitle = model.SiteTitle;
                settings.SiteDescription = model.SiteDescription;
                settings.Phone = model.Phone;
                settings.Email = model.Email;
                settings.Address = model.Address;
                settings.EconomicCode = model.EconomicCode;
                settings.PostalCode = model.PostalCode;
                settings.WorkingHours = model.WorkingHours;
                settings.InstagramUrl = model.InstagramUrl;
                settings.TelegramUrl = model.TelegramUrl;
                settings.WhatsAppUrl = model.WhatsAppUrl;
                settings.baleUrl = model.baleUrl;
                settings.rubikaUrl = model.rubikaUrl;
                settings.eitaaUrl = model.eitaaUrl;
                settings.soroushUrl = model.soroushUrl;
                settings.CertificationsJson = SerializeCertifications();
                settings.AboutShortText = model.AboutShortText;
                settings.FooterCopyrightText = model.FooterCopyrightText;
                settings.IsGalleryEnabled = model.IsGalleryEnabled;
                settings.PromoEnabled = model.PromoEnabled;
                settings.PromoBadgeText = model.PromoBadgeText;
                settings.PromoTitle = model.PromoTitle;
                settings.PromoText = model.PromoText;
                settings.PromoButton1Text = model.PromoButton1Text;
                settings.PromoButton1Url = model.PromoButton1Url;
                settings.PromoButton2Text = model.PromoButton2Text;
                settings.PromoButton2Url = model.PromoButton2Url;
                settings.PromoBgColor = model.PromoBgColor;
                settings.PromoBadgeTextColor = model.PromoBadgeTextColor;
                settings.PromoTitleColor = model.PromoTitleColor;
                settings.PromoTextColor = model.PromoTextColor;
                settings.PromoButton1BgColor = model.PromoButton1BgColor;
                settings.PromoButton1TextColor = model.PromoButton1TextColor;
                settings.PromoButton2BgColor = model.PromoButton2BgColor;
                settings.PromoButton2TextColor = model.PromoButton2TextColor;
                settings.PromoSpecialEffectEnabled = model.PromoSpecialEffectEnabled;

                // ══════════════════════════════════════════════════════════
                // ✅ تبدیل چک‌باکس‌های انتخاب‌شده به JSON
                // ══════════════════════════════════════════════════════════
                if (selectedQuickLinks != null && selectedQuickLinks.Any())
                {
                    var availablePages = GetAvailablePages();
                    var quickLinksList = availablePages
                        .Where(p => selectedQuickLinks.Contains(p.Value))
                        .Select(p => new { Title = p.Key, Url = p.Value })
                        .ToList();

                    settings.QuickLinksJson = JsonSerializer.Serialize(quickLinksList);
                }
                else
                {
                    settings.QuickLinksJson = "[]"; // اگر هیچکدام تیک نخورد
                }
                // ══════════════════════════════════════════════════════════

                // ══════════════════════════════════════════════════════════
                // ✅ خواندن اسلایدرهای پرومو از فرم و تبدیل به JSON
                // ══════════════════════════════════════════════════════════
                var sliderImages = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    if (Request.Form.TryGetValue($"PromoSliderImage_{i}", out var formValue))
                    {
                        var val = formValue.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            sliderImages.Add(val);
                        }
                    }
                }

                settings.PromoSliderImages = sliderImages.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(sliderImages)
                    : null;
                // ══════════════════════════════════════════════════════════
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = "تنظیمات سایت با موفقیت ذخیره شد.";
                return RedirectToAction(nameof(Index));
            }

            // در صورت خطای Validation، دوباره لیست‌ها را پر کن
            ViewBag.AvailableLinks = GetAvailablePages();
            ViewBag.CurrentQuickLinks = new List<string>();
            ViewBag.Certifications = ParseCertifications(model.CertificationsJson);

            return View(model);
        }

        // ══════════════════════════════════════════════════════════
        // ✅ گواهینامه‌ها: خواندن/نوشتن JSON به‌جای ادمین (ویرایشگر بصری)
        // ══════════════════════════════════════════════════════════
        private static List<CertificationItem> ParseCertifications(string? json)
        {
            var result = new List<CertificationItem>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                var parsed = JsonSerializer.Deserialize<List<CertificationItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) result.AddRange(parsed);
            }
            catch
            {
                // JSON خراب از نسخه‌های قبلی: با لیست خالی شروع می‌کنیم تا صفحه کرش نکند
            }
            return result;
        }

        private string SerializeCertifications()
        {
            // فیلدهای Certifications[n].Title / ImagePath / Link / Alt از فرم خوانده می‌شوند
            var items = new List<CertificationItem>();
            var maxIndex = -1;
            foreach (var kv in Request.Form)
            {
                var m = System.Text.RegularExpressions.Regex.Match(kv.Key, @"^Certifications\[(\d+)\]\.(Title|ImagePath|Link|Alt)$");
                if (m.Success)
                {
                    var idx = int.Parse(m.Groups[1].Value);
                    if (idx > maxIndex) maxIndex = idx;
                }
            }

            for (int i = 0; i <= maxIndex; i++)
            {
                var title = Request.Form[$"Certifications[{i}].Title"].ToString().Trim();
                var imagePath = Request.Form[$"Certifications[{i}].ImagePath"].ToString().Trim();
                var link = Request.Form[$"Certifications[{i}].Link"].ToString().Trim();
                var alt = Request.Form[$"Certifications[{i}].Alt"].ToString().Trim();
                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(imagePath)) continue; // ردیف خالی
                items.Add(new CertificationItem
                {
                    Title = title,
                    ImagePath = imagePath,
                    Link = link,
                    Alt = string.IsNullOrWhiteSpace(alt) ? title : alt
                });
            }
            return JsonSerializer.Serialize(items);
        }
        private List<KeyValuePair<string, string>> GetAvailablePages()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("🏠 خانه", "/"),
                new KeyValuePair<string, string>("🍰 محصولات", "/Products"),
                new KeyValuePair<string, string>("📦 آموزش شیرینی پزی", "/Educational"),
                new KeyValuePair<string, string>("📖 درباره ما", "/Home/AboutUs"),
                new KeyValuePair<string, string>("📞 تماس با ما", "/Home/Contact"),
                // برای اضافه کردن صفحه جدید، فقط یک خط به این لیست اضافه کنید:
                // new KeyValuePair<string, string>("عنوان صفحه", "/آدرس-صفحه")
            };
        }
    }
}