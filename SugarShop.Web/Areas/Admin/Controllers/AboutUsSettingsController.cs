using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Areas.Admin.ViewModels;
using System;
using System.Text.Json;
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

            // ✅ ساخت لیست بصری از روی JSON (بدون نیاز به ویرایش دستی JSON توسط ادمین)
            try
            {
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var stored = JsonSerializer.Deserialize<List<AboutUsValueItem>>(vm.ValuesJson, jsonOptions);
                vm.Values = stored ?? new List<AboutUsValueItem>();
            }
            catch
            {
                _logger.LogWarning("JSON نامعتبر در ValuesJson درباره ما؛ با لیست خالی شروع می‌شود.");
                vm.Values = new List<AboutUsValueItem>();
            }

            return View(vm);
        }

        // ============================================================
        // ✅ ذخیره فرم (POST) -> آدرس: /Admin/AboutUsSettings
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AboutUsViewModel model)
        {
            try
            {
                _logger.LogInformation("=== شروع ذخیره‌سازی تنظیمات درباره ما ===");

                var settings = await _context.AboutUsSettings.FirstOrDefaultAsync();

                if (settings == null)
                {
                    settings = new AboutUsSetting();
                    _context.AboutUsSettings.Add(settings);
                }

                // ✅ نگاشت مستقیم و ایمن از ViewModel به Entity (بدون نیاز به Request.Form)
                settings.HeroTitle = model.HeroTitle;
                settings.HeroDescription = model.HeroDescription;
                settings.StoryTitle = model.StoryTitle;
                settings.StoryContent = model.StoryContent;
                settings.VideoTitle = model.VideoTitle;
                settings.ValuesJson = SerializeValues(model.Values);

                // مسیرهای تصاویر و ویدئو (اگر خالی نباشند به‌روز می‌شوند)
                if (!string.IsNullOrWhiteSpace(model.HeroImagePath))
                    settings.HeroImagePath = model.HeroImagePath;

                if (!string.IsNullOrWhiteSpace(model.StoryImagePath))
                    settings.StoryImagePath = model.StoryImagePath;

                if (!string.IsNullOrWhiteSpace(model.VideoPath))
                    settings.VideoPath = model.VideoPath;

                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ تنظیمات با موفقیت ذخیره شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطا در ذخیره‌سازی تنظیمات درباره ما");
                TempData["Error"] = $"❌ خطا در ذخیره‌سازی: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// تبدیل لیست کارت‌های ارزش به JSON برای ذخیره در دیتابیس.
        /// فقط آیتم‌هایی که عنوان یا توضیح دارند ذخیره می‌شوند.
        /// </summary>
        private static string SerializeValues(List<AboutUsValueItem>? values)
        {
            var clean = (values ?? new List<AboutUsValueItem>())
                .Where(v => !string.IsNullOrWhiteSpace(v.Title) || !string.IsNullOrWhiteSpace(v.Desc))
                .Select(v => new AboutUsValueItem
                {
                    Icon = string.IsNullOrWhiteSpace(v.Icon) ? "bi-star" : v.Icon.Trim(),
                    Title = v.Title?.Trim() ?? "",
                    Desc = v.Desc?.Trim() ?? ""
                })
                .ToList();

            return JsonSerializer.Serialize(clean);
        }
    }
}