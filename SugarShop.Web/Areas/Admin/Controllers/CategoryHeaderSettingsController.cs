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

        public CategoryHeaderSettingsController(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId)
        {
            var allCategories = await _catalogDb.Categories
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            ViewBag.AllCategories = allCategories;

            if (!categoryId.HasValue && allCategories.Any())
            {
                categoryId = allCategories.First().Id;
            }

            ViewBag.SelectedCategoryId = categoryId;

            var settings = await _salesDb.CategoryHeaderSettings
                .FirstOrDefaultAsync(s => s.CategoryId == categoryId);

            if (settings == null && categoryId.HasValue)
            {
                settings = new CategoryHeaderSetting
                {
                    CategoryId = categoryId.Value,
                    BackgroundColor = "#ffffff",
                    TextColor = "#2c3e50",
                    Height = 200,
                    IsEnabled = true
                };
            }

            return View(settings ?? new CategoryHeaderSetting());
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Index()
        //{
        //    try
        //    {
        //        // ✅ خواندن ایمن CategoryId با مدیریت null
        //        var categoryIdStr = Request.Form["CategoryId"].ToString();

        //        if (string.IsNullOrWhiteSpace(categoryIdStr) || !int.TryParse(categoryIdStr, out var categoryId))
        //        {
        //            TempData["Error"] = "لطفاً ابتدا یک دسته‌بندی انتخاب کنید.";
        //            return RedirectToAction(nameof(Index));
        //        }

        //        // ✅ خواندن ایمن سایر فیلدها
        //        var defaultTitle = Request.Form["DefaultTitle"].ToString();
        //        var defaultSubtitle = Request.Form["DefaultSubtitle"].ToString();
        //        var boxSelectionSubtitle = Request.Form["BoxSelectionSubtitle"].ToString();
        //        var backgroundColor = Request.Form["BackgroundColor"].ToString();
        //        var textColor = Request.Form["TextColor"].ToString();
        //        var heightStr = Request.Form["Height"].ToString();
        //        var isEnabled = Request.Form["IsEnabled"] == "on" || Request.Form["IsEnabled"] == "true";
        //        var useBackgroundImage = Request.Form["UseBackgroundImage"] == "on" || Request.Form["UseBackgroundImage"] == "true";
        //        var bgImagePath = Request.Form["BackgroundImagePath"].ToString();

        //        // ✅ تبدیل ایمن Height
        //        int height = 200;
        //        if (!string.IsNullOrWhiteSpace(heightStr) && int.TryParse(heightStr, out var parsedHeight))
        //        {
        //            height = parsedHeight;
        //        }

        //        var settings = await _salesDb.CategoryHeaderSettings
        //            .FirstOrDefaultAsync(s => s.CategoryId == categoryId);

        //        if (settings == null)
        //        {
        //            settings = new CategoryHeaderSetting { CategoryId = categoryId };
        //            _salesDb.CategoryHeaderSettings.Add(settings);
        //        }

        //        // ✅ ذخیره ایمن مقادیر (اگر خالی بودند، null ذخیره کن)
        //        settings.DefaultTitle = string.IsNullOrWhiteSpace(defaultTitle) ? null : defaultTitle;
        //        settings.DefaultSubtitle = string.IsNullOrWhiteSpace(defaultSubtitle) ? null : defaultSubtitle;
        //        settings.BoxSelectionSubtitle = string.IsNullOrWhiteSpace(boxSelectionSubtitle) ? null : boxSelectionSubtitle;
        //        settings.BackgroundColor = string.IsNullOrWhiteSpace(backgroundColor) ? "#ffffff" : backgroundColor;
        //        settings.TextColor = string.IsNullOrWhiteSpace(textColor) ? "#2c3e50" : textColor;
        //        settings.Height = height;
        //        settings.IsEnabled = isEnabled;
        //        settings.UseBackgroundImage = useBackgroundImage;
        //        settings.BackgroundImagePath = string.IsNullOrWhiteSpace(bgImagePath) ? null : bgImagePath;
        //        settings.UpdatedAt = DateTime.UtcNow;

        //        await _salesDb.SaveChangesAsync();
        //        TempData["Success"] = "✅ تنظیمات با موفقیت ذخیره شد!";
        //    }
        //    catch (Exception ex)
        //    {
        //        TempData["Error"] = $"خطا: {ex.Message}";
        //    }

        //    var redirectCategoryId = int.TryParse(Request.Form["CategoryId"].ToString(), out var catId) ? catId : (int?)null;
        //    return RedirectToAction(nameof(Index), new { categoryId = redirectCategoryId });
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CategoryHeaderSetting model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "برخی فیلدها به‌درستی وارد نشده‌اند.";
                return RedirectToAction(nameof(Index), new { categoryId = model.CategoryId });
            }

            try
            {
                // پیدا کردن تنظیمات موجود
                var settings = await _salesDb.CategoryHeaderSettings
                    .FirstOrDefaultAsync(s => s.CategoryId == model.CategoryId);

                if (settings == null)
                {
                    settings = new CategoryHeaderSetting
                    {
                        CategoryId = model.CategoryId
                    };
                    _salesDb.CategoryHeaderSettings.Add(settings);
                }

                // انتقال مقادیر از مدل به Entity
                settings.DefaultTitle = string.IsNullOrWhiteSpace(model.DefaultTitle) ? null : model.DefaultTitle;
                settings.DefaultSubtitle = string.IsNullOrWhiteSpace(model.DefaultSubtitle) ? null : model.DefaultSubtitle;
                settings.BoxSelectionSubtitle = string.IsNullOrWhiteSpace(model.BoxSelectionSubtitle) ? null : model.BoxSelectionSubtitle;

                settings.BackgroundColor = string.IsNullOrWhiteSpace(model.BackgroundColor) ? "#ffffff" : model.BackgroundColor;
                settings.TextColor = string.IsNullOrWhiteSpace(model.TextColor) ? "#2c3e50" : model.TextColor;

                settings.Height = model.Height;
                settings.IsEnabled = model.IsEnabled;
                settings.UseBackgroundImage = model.UseBackgroundImage;

                settings.BackgroundImagePath = string.IsNullOrWhiteSpace(model.BackgroundImagePath) ? null : model.BackgroundImagePath;

                settings.UpdatedAt = DateTime.UtcNow;

                await _salesDb.SaveChangesAsync();

                TempData["Success"] = "تنظیمات با موفقیت ذخیره شد!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در ذخیره‌سازی: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { categoryId = model.CategoryId });
        }

    }
}