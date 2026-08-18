using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    public class SweetItemController : Controller
    {
        private readonly SugarShopCatalogDbContext _db;

        public SweetItemController(SugarShopCatalogDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int? categoryId)
        {
            var query = _db.SweetItems.AsNoTracking();

            if (categoryId.HasValue)
                query = query.Where(s => s.CategoryId == categoryId.Value);

            var items = await query
                .Include(s => s.Category)
                .OrderByDescending(s => s.SortOrder)
                .ToListAsync();

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var item = await _db.SweetItems
                .AsNoTracking()
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (item == null) return NotFound();
            return View(item);
        }
    }
}