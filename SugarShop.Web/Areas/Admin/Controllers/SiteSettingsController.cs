using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/[controller]")]
    public class SiteSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public SiteSettingsController(SugarShopSalesDbContext context)
        {
            _context = context;
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
                if (!string.IsNullOrEmpty(model.LogoPath))
                    settings.LogoPath = model.LogoPath;
                if (!string.IsNullOrEmpty(model.FaviconPath))
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
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "تنظیمات سایت با موفقیت ذخیره شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
    }
}