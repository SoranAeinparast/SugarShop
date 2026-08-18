using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Web.SessionModels;

public class ProductController : Controller
{
    private readonly SugarShopCatalogDbContext _db;

    public ProductController(SugarShopCatalogDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? categoryId)
    {
        var query = _db.Set<Product>().AsNoTracking();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        var products = await query
            .Include(p => p.Category)
            .OrderByDescending(p => p.SortOrder)
            .ToListAsync();

        return View(products);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Set<Product>()
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return NotFound();
        return View(product);
    }
}