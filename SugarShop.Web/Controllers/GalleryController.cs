using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers
{
    public class GalleryController : Controller
    {
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly SugarShopCatalogDbContext _catalogDb;

        public GalleryController(SugarShopSalesDbContext salesDb, SugarShopCatalogDbContext catalogDb)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _salesDb.SiteSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SiteSetting { IsGalleryEnabled = true };
                _salesDb.SiteSettings.Add(settings);
                await _salesDb.SaveChangesAsync();
            }

            if (!settings.IsGalleryEnabled)
            {
                ViewBag.Message = "گالری موقتاً غیرفعال شده است. لطفاً بعداً مراجعه کنید.";
                return View(Enumerable.Empty<GalleryItem>());
            }

            // ✅ خواندن تنظیمات هدر گالری
            var headerSettings = await _salesDb.GalleryHeaderSettings.FirstOrDefaultAsync();
            ViewBag.HeaderSettings = headerSettings;

            var items = await _catalogDb.GalleryItems
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            return View(items);
        }
    }
}