using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/CategoryHeaderSettings")]
    public class CategoryHeaderSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly ILogger<CategoryHeaderSettingsController> _logger;

        public CategoryHeaderSettingsController(SugarShopSalesDbContext salesDb, SugarShopCatalogDbContext catalogDb, ILogger<CategoryHeaderSettingsController> logger)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId)
        {
            var allCategories = await _catalogDb.Categories.OrderBy(c => c.SortOrder).ToListAsync();
            ViewBag.AllCategories = allCategories;

            if (!categoryId.HasValue || categoryId.Value <= 0)
            {
                categoryId = allCategories.FirstOrDefault()?.Id;
            }
            ViewBag.SelectedCategoryId = categoryId ?? 0;

            // اگر هیچ دسته‌بندی وجود نداشته باشد، از خطای NullReference جلوگیری می‌کنیم
            if (!categoryId.HasValue)
            {
                return View(new CategoryHeaderSetting
                {
                    CategoryId = 0,
                    BackgroundColor = "#ffffff",
                    TextColor = "#2c3e50",
                    Height = 200,
                    IsEnabled = true,
                    UseBackgroundImage = false
                });
            }

            var settings = await _salesDb.CategoryHeaderSettings
                .Where(s => s.CategoryId == categoryId.Value)
                .OrderByDescending(s => s.UpdatedAt)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new CategoryHeaderSetting
                {
                    CategoryId = categoryId!.Value,
                    BackgroundColor = "#ffffff",
                    TextColor = "#2c3e50",
                    Height = 200,
                    IsEnabled = true,
                    UseBackgroundImage = false
                };
            }

            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CategoryHeaderSetting model)
        {
            // ✅ خواندن اجباری CategoryId برای جلوگیری از خطای "نامعتبر"
            int finalCategoryId = model.CategoryId;
            if (finalCategoryId <= 0)
            {
                int.TryParse(Request.Form["CategoryId"], out finalCategoryId);
            }

            if (finalCategoryId <= 0)
            {
                TempData["Error"] = "دسته‌بندی نامعتبر است (CategoryId = 0).";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // ✅ انتخاب دقیقاً همان ردیفی که در صفحه نمایش داده می‌شود (جدیدترین ردیف)
                // اگر رکوردهای تکراری از نسخه‌های قبلی باقی مانده باشند، به‌روزرسانی روی ردیف
                // درست انجام شود و تغییرات «اعمال نشده» به نظر نرسند.
                var settings = await _salesDb.CategoryHeaderSettings
                    .Where(s => s.CategoryId == finalCategoryId)
                    .OrderByDescending(s => s.UpdatedAt)
                    .ThenByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                if (settings == null)
                {
                    settings = new CategoryHeaderSetting { CategoryId = finalCategoryId };
                    _salesDb.CategoryHeaderSettings.Add(settings);
                }

                settings.DefaultTitle = string.IsNullOrWhiteSpace(model.DefaultTitle) ? null : model.DefaultTitle.Trim();
                settings.DefaultSubtitle = string.IsNullOrWhiteSpace(model.DefaultSubtitle) ? null : model.DefaultSubtitle.Trim();
                settings.BoxSelectionSubtitle = string.IsNullOrWhiteSpace(model.BoxSelectionSubtitle) ? null : model.BoxSelectionSubtitle.Trim();
                settings.BackgroundColor = string.IsNullOrWhiteSpace(model.BackgroundColor) ? "#ffffff" : model.BackgroundColor.Trim();
                settings.TextColor = string.IsNullOrWhiteSpace(model.TextColor) ? "#2c3e50" : model.TextColor.Trim();
                settings.Height = model.Height > 0 ? model.Height : 200;

                settings.IsEnabled = model.IsEnabled;

                // ✅ منطق صحیح تصویر پس‌زمینه:
                // - اگر مسیر جدیدی انتخاب شده باشد → ذخیره و فعال‌سازی خودکار
                // - اگر مسیر جدیدی انتخاب نشده باشد → تصویر قبلی حفظ می‌شود (پاک نمی‌شود!)
                // - تصویر فقط زمانی حذف می‌شود که تیک «استفاده از تصویر» برداشته شود
                if (!string.IsNullOrWhiteSpace(model.BackgroundImagePath) && model.BackgroundImagePath != settings.BackgroundImagePath)
                {
                    // کاربر تصویر جدیدی انتخاب کرده → ذخیره و فعال‌سازی خودکار
                    settings.BackgroundImagePath = model.BackgroundImagePath.Trim();
                    settings.UseBackgroundImage = true;
                }
                else if (model.UseBackgroundImage)
                {
                    // تصویر جدیدی انتخاب نشده ولی تیک فعال است → تصویر قبلی حفظ می‌شود
                    settings.UseBackgroundImage = !string.IsNullOrWhiteSpace(settings.BackgroundImagePath);
                }
                else
                {
                    // تیک استفاده از تصویر برداشته شده → تصویر حذف می‌شود
                    settings.BackgroundImagePath = null;
                    settings.UseBackgroundImage = false;
                }

                settings.UpdatedAt = DateTime.UtcNow;
                await _salesDb.SaveChangesAsync();

                // ✅ پاک‌سازی رکوردهای تکراری همان دسته (باقی‌مانده از نسخه‌های قبلی)
                var duplicates = await _salesDb.CategoryHeaderSettings
                    .Where(s => s.CategoryId == finalCategoryId && s.Id != settings.Id)
                    .ToListAsync();
                if (duplicates.Count > 0)
                {
                    _salesDb.CategoryHeaderSettings.RemoveRange(duplicates);
                    await _salesDb.SaveChangesAsync();
                }

                TempData["Success"] = "✅ تنظیمات با موفقیت در دیتابیس ذخیره شد!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ذخیره‌سازی تنظیمات هدر دسته‌بندی برای CategoryId={CategoryId}", finalCategoryId);
                TempData["Error"] = $"❌ خطا در ذخیره‌سازی: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { categoryId = finalCategoryId });
        }
    }
}