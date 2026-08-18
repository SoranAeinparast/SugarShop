using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.SessionModels;
using SugarShop.Web.ViewModels;
using System.Diagnostics;

namespace SugarShop.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly SugarShopSalesDbContext _salesDb;

        public HomeController(SugarShopCatalogDbContext catalogDb, SugarShopSalesDbContext salesDb)
        {
            _catalogDb = catalogDb;
            _salesDb = salesDb;
        }

        public async Task<IActionResult> Index(string? subscribed)
        {
            var splashSettings = await _salesDb.SplashSettings.FirstOrDefaultAsync();

            bool showSplash = false;
            if (splashSettings != null && splashSettings.IsEnabled && !string.IsNullOrEmpty(splashSettings.VideoPath))
            {
                bool splashShown = HttpContext.Session.GetString("SplashShown") == "true";
                showSplash = !splashShown;
            }

            ViewBag.ShowSplash = showSplash;
            ViewBag.SplashVideoPath = splashSettings?.VideoPath ?? "/videos/garmsar.mp4";
            ViewBag.SplashButtonText = splashSettings?.ButtonText ?? "ورود";
            ViewBag.SplashIsMuted = splashSettings?.IsMuted ?? true;

            var cats = await _catalogDb.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Take(12)
                .Select(c => new CategoryItemVm
                {
                    Id = c.Id,
                    TitleFa = c.TitleFa,
                    ImagePath = c.ImagePath,
                    Slug = c.Slug
                })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var sliders = await _catalogDb.Sliders
                .Where(s =>
                    s.IsActive &&
                    (s.StartAt == null || s.StartAt <= now) &&
                    (s.EndAt == null || s.EndAt >= now))
                .OrderBy(s => s.SortOrder)
                .Select(s => new SliderItemVm
                {
                    Id = s.Id,
                    Title = s.Title,
                    Subtitle = s.Subtitle,
                    ImagePath = s.ImagePath,
                    ButtonText = s.ButtonText,
                    ButtonUrl = s.ButtonUrl,
                    SortOrder = s.SortOrder,
                    IsVideo = s.IsVideo
                })
                .ToListAsync();

            var siteSetting = await _salesDb.SiteSettings.FirstOrDefaultAsync();

            var vm = new HomeIndexVm
            {
                Categories = cats,
                Sliders = sliders,
                SiteSetting = siteSetting
            };

            if (!string.IsNullOrEmpty(subscribed))
            {
                ViewBag.SubscribeMessage = subscribed switch
                {
                    "success" => "✅ با موفقیت عضو خبرنامه شدید.",
                    "exists" => "ℹ️ این ایمیل قبلاً ثبت شده است.",
                    "active" => "✅ اشتراک شما مجدداً فعال شد.",
                    _ => null
                };
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetSplashShown()
        {
            HttpContext.Session.SetString("SplashShown", "true");
            return Ok();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Aboutus()
        {
            var settings = await _salesDb.AboutUsSettings.FirstOrDefaultAsync() ?? new AboutUsSetting();
            return View(settings);
        }
    }
}