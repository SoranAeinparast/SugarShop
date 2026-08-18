using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Areas.Admin.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    public class AboutUsSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly ILogger<AboutUsSettingsController> _logger;

        public AboutUsSettingsController(
            SugarShopSalesDbContext context,
            ILogger<AboutUsSettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // ✅ نمایش فرم (GET) -> آدرس: /Admin/AboutUsSettings
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.AboutUsSettings.FirstOrDefaultAsync();

            var vm = new AboutUsViewModel
            {
                Id = settings?.Id ?? 0,
                HeroTitle = settings?.HeroTitle ?? "درباره شیرینی سرا",
                HeroDescription = settings?.HeroDescription ?? "ما با عشق، بهترین لحظات شیرین زندگی شما را می‌سازیم.",
                StoryTitle = settings?.StoryTitle ?? "داستان ما",
                StoryContent = settings?.StoryContent ?? "متن پیش‌فرض داستان ما...",
                VideoTitle = settings?.VideoTitle ?? "نگاهی به آشپزخانه ما",
                ValuesJson = settings?.ValuesJson ?? "[]",
                HeroImagePath = settings?.HeroImagePath,
                StoryImagePath = settings?.StoryImagePath,
                VideoPath = settings?.VideoPath
            };

            return View(vm);
        }

        // ============================================================
        // ✅ ذخیره فرم (POST) -> آدرس: /Admin/AboutUsSettings
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save()
        {
            try
            {
                _logger.LogInformation("=== شروع ذخیره‌سازی تنظیمات درباره ما ===");

                // خواندن مسیرهای متنی (انتخاب‌شده از مودال Media Picker)
                var heroImagePath = Request.Form["HeroImagePath"].ToString();
                var storyImagePath = Request.Form["StoryImagePath"].ToString();
                var videoPath = Request.Form["VideoPath"].ToString();

                // خواندن فیلدهای متنی دیگر
                var heroTitle = Request.Form["HeroTitle"].ToString();
                var heroDescription = Request.Form["HeroDescription"].ToString();
                var storyTitle = Request.Form["StoryTitle"].ToString();
                var storyContent = Request.Form["StoryContent"].ToString();
                var videoTitle = Request.Form["VideoTitle"].ToString();
                var valuesJson = Request.Form["ValuesJson"].ToString();

                var settings = await _context.AboutUsSettings.FirstOrDefaultAsync();
                bool isNew = (settings == null);

                if (isNew)
                {
                    settings = new AboutUsSetting();
                    _context.AboutUsSettings.Add(settings);
                }

                // مسیرهای تصاویر و ویدئو (انتخاب‌شده از Media Picker)
                if (!string.IsNullOrWhiteSpace(heroImagePath))
                    settings.HeroImagePath = heroImagePath;

                if (!string.IsNullOrWhiteSpace(storyImagePath))
                    settings.StoryImagePath = storyImagePath;

                if (!string.IsNullOrWhiteSpace(videoPath))
                    settings.VideoPath = videoPath;

                // به‌روزرسانی فیلدهای متنی
                settings.HeroTitle = heroTitle;
                settings.HeroDescription = heroDescription;
                settings.StoryTitle = storyTitle;
                settings.StoryContent = storyContent;
                settings.VideoTitle = videoTitle;
                settings.ValuesJson = string.IsNullOrEmpty(valuesJson) ? "[]" : valuesJson;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ تنظیمات با موفقیت ذخیره شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطا در ذخیره‌سازی");
                TempData["Error"] = $"❌ خطا: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}