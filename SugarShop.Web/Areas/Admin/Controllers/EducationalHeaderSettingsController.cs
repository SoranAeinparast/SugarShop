using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using System;
using System.Threading.Tasks;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/EducationalHeaderSettings")]
    public class EducationalHeaderSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly ILogger<EducationalHeaderSettingsController> _logger;

        public EducationalHeaderSettingsController(SugarShopSalesDbContext salesDb, ILogger<EducationalHeaderSettingsController> logger)
        {
            _salesDb = salesDb;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _salesDb.EducationalHeaderSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new EducationalHeaderSetting();
                _salesDb.EducationalHeaderSettings.Add(settings);
                await _salesDb.SaveChangesAsync();
            }
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(EducationalHeaderSetting model)
        {
            try
            {
                var settings = await _salesDb.EducationalHeaderSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new EducationalHeaderSetting();
                    _salesDb.EducationalHeaderSettings.Add(settings);
                }

                settings.Title = string.IsNullOrWhiteSpace(model.Title) ? "آموزش‌های شیرینی‌پزی" : model.Title.Trim();
                settings.Subtitle = string.IsNullOrWhiteSpace(model.Subtitle)
                    ? "آموزش‌های حرفه‌ای و کاربردی برای شیرینی‌پزی خانگی" // پیش‌فرض موجودیت — نه null
                    : model.Subtitle.Trim();
                settings.BackgroundColor = string.IsNullOrWhiteSpace(model.BackgroundColor)
                    ? "linear-gradient(135deg, #f5e6d3 0%, #d4a056 100%)"
                    : model.BackgroundColor.Trim();
                settings.TextColor = string.IsNullOrWhiteSpace(model.TextColor) ? "#3e2723" : model.TextColor.Trim();
                settings.Height = model.Height > 0 ? model.Height : 300;
                settings.IsEnabled = model.IsEnabled;

                // ✅ تصویر جدید → ذخیره؛ تصویر قبلی بدون انتخاب جدید حفظ می‌شود
                if (!string.IsNullOrWhiteSpace(model.BackgroundImagePath) && model.BackgroundImagePath != settings.BackgroundImagePath)
                {
                    settings.BackgroundImagePath = model.BackgroundImagePath.Trim();
                }

                settings.UpdatedAt = DateTime.UtcNow;
                await _salesDb.SaveChangesAsync();

                TempData["Success"] = "✅ تنظیمات هدر صفحه آموزش با موفقیت ذخیره شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ذخیره‌سازی تنظیمات هدر صفحه آموزش");
                TempData["Error"] = $"❌ خطا در ذخیره‌سازی: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
