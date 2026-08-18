using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
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
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SiteSetting model)
        {
            if (ModelState.IsValid)
            {
                var settings = await _context.SiteSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new SiteSetting();
                    _context.SiteSettings.Add(settings);
                }
                settings.LogoPath = model.LogoPath;
                settings.FaviconPath = model.FaviconPath;
                settings.SiteTitle = model.SiteTitle;
                settings.SiteDescription = model.SiteDescription;
                settings.Phone = model.Phone;
                settings.Email = model.Email;
                settings.Address = model.Address;
                settings.WorkingHours = model.WorkingHours;
                settings.InstagramUrl = model.InstagramUrl;
                settings.TelegramUrl = model.TelegramUrl;
                settings.WhatsAppUrl = model.WhatsAppUrl;
                settings.baleUrl = model.baleUrl;
                settings.rubikaUrl = model.rubikaUrl;
                settings.eitaaUrl = model.eitaaUrl;
                settings.soroushUrl = model.soroushUrl;
                settings.CertificationsJson = model.CertificationsJson;
                settings.AboutShortText = model.AboutShortText;
                settings.QuickLinksJson = model.QuickLinksJson;
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
                settings.PromoSliderImages = model.PromoSliderImages;

                settings.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تنظیمات سایت با موفقیت ذخیره شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
    }
}