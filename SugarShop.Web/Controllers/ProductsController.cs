using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class ProductsController : Controller
    {
        private readonly SugarShopCatalogDbContext _context;

        public ProductsController(SugarShopCatalogDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();
            return View(products);
        }
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.Products.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "محصول با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var existing = await _context.Products.FindAsync(id);
                if (existing == null) return NotFound();

                existing.TitleFa = model.TitleFa;
                existing.Slug = model.Slug;
                existing.CategoryId = model.CategoryId;
                existing.Price = model.Price;
                existing.WeightGrams = model.WeightGrams;
                existing.Inventory = model.Inventory;
                existing.IsActive = model.IsActive;
                existing.SortOrder = model.SortOrder;
                existing.Description = model.Description;
                existing.ImagePath = model.ImagePath;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "محصول ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "محصول حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}