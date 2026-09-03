using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public CategoryHeaderSettingsController(SugarShopSalesDbContext salesDb, SugarShopCatalogDbContext catalogDb)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
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

            var settings = await _salesDb.CategoryHeaderSettings
                .Where(s => s.CategoryId == categoryId.Value)
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new CategoryHeaderSetting
                {
                    CategoryId = categoryId.Value,
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
                var settings = await _salesDb.CategoryHeaderSettings
                    .FirstOrDefaultAsync(s => s.CategoryId == finalCategoryId);

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
                settings.UseBackgroundImage = model.UseBackgroundImage;

                if (!string.IsNullOrWhiteSpace(model.BackgroundImagePath))
                {
                    settings.UseBackgroundImage = true;
                    settings.BackgroundImagePath = model.BackgroundImagePath.Trim();
                }
                else
                {
                    settings.BackgroundImagePath = null;
                }

                settings.UpdatedAt = DateTime.UtcNow;
                await _salesDb.SaveChangesAsync();
                TempData["Success"] = "✅ تنظیمات با موفقیت در دیتابیس ذخیره شد!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ خطا در ذخیره‌سازی: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { categoryId = finalCategoryId });
        }
    }
}