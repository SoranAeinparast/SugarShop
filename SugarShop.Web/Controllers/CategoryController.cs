using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.SessionModels;
using SugarShop.Web.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
    public class CategoryController : Controller
    {
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly SugarShopSalesDbContext _salesDb; // ✅ اضافه شد

        public CategoryController(
            SugarShopCatalogDbContext catalogDb,
            SugarShopSalesDbContext salesDb) // ✅ اضافه شد
        {
            _catalogDb = catalogDb;
            _salesDb = salesDb; // ✅ اضافه شد
        }

        [HttpGet("/Category/{slug}")]
        public async Task<IActionResult> Index(string slug)
        {
            var category = await _catalogDb.Categories
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);

            if (category == null) return NotFound();

            // ✅ خواندن تنظیمات هدر مخصوص این دسته‌بندی از دیتابیس Sales
            var headerSettings = await _salesDb.CategoryHeaderSettings
                .FirstOrDefaultAsync(s => s.CategoryId == category.Id);

            // ✅ ارسال به ویو از طریق ViewBag
            ViewBag.HeaderSettings = headerSettings;

            var products = new List<ProductCardViewModel>();

            if (category.RequiresBoxSelection)
            {
                var sweets = await _catalogDb.SweetItems
                    .Where(s => s.CategoryId == category.Id && s.IsActive)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new ProductCardViewModel
                    {
                        Id = s.Id,
                        Title = s.TitleFa,
                        ImagePath = s.ImagePath,
                        Description = s.Description,
                        IsActive = s.IsActive,
                        Inventory = s.InventoryCount,
                        IsSweet = true,
                        ApproxWeightGrams = s.ApproxWeightGrams,
                        PricePerKg = s.PricePerKg
                    })
                    .ToListAsync();
                products.AddRange(sweets);
            }
            else
            {
                var regularProducts = await _catalogDb.Products
                    .Where(p => p.CategoryId == category.Id && p.IsActive)
                    .OrderBy(p => p.SortOrder)
                    .Select(p => new ProductCardViewModel
                    {
                        Id = p.Id,
                        Title = p.TitleFa,
                        ImagePath = p.ImagePath,
                        Description = p.Description,
                        IsActive = p.IsActive,
                        Inventory = p.Inventory,
                        IsSweet = false,
                        UnitPrice = p.Price,
                        WeightGrams = (int?)p.WeightGrams
                    })
                    .ToListAsync();
                products.AddRange(regularProducts);
            }

            var model = new CategoryProductsViewModel
            {
                CategoryId = category.Id,
                CategoryTitle = category.TitleFa,
                CategorySlug = category.Slug,
                RequiresBoxSelection = category.RequiresBoxSelection,
                Products = products
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetBoxPanel()
        {
            var state = HttpContext.Session.GetBoxSessionState();
            var boxTypes = await _catalogDb.BoxTypes
                .Where(b => b.IsActive)
                .OrderBy(b => b.SortOrder)
                .ToListAsync();
            var selectedBox = boxTypes.FirstOrDefault(b => b.Id == state.SelectedBoxTypeId);
            ViewBag.SelectedBoxType = selectedBox;
            ViewBag.BoxTypes = boxTypes;
            return PartialView("_BoxPanel", state);
        }
    }
}