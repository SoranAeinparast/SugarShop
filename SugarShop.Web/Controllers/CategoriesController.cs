using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class CategoriesController : Controller
    {
        private readonly SugarShopCatalogDbContext _context;

        public CategoriesController(SugarShopCatalogDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            return View(categories);
        }
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model)
        {
            if (await _context.Categories.AnyAsync(c => c.Slug == model.Slug))
            {
                ModelState.AddModelError("Slug", "این Slug قبلاً برای دسته‌بندی دیگری استفاده شده است.");
            }

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.Categories.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "دسته‌بندی با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category model)
        {
            if (id != model.Id)
                return NotFound();

            if (await _context.Categories.AnyAsync(c => c.Slug == model.Slug && c.Id != id))
            {
                ModelState.AddModelError("Slug", "این Slug قبلاً برای دسته‌بندی دیگری استفاده شده است.");
            }

            if (ModelState.IsValid)
            {
                var existing = await _context.Categories.FindAsync(id);
                if (existing == null)
                    return NotFound();

                existing.TitleFa = model.TitleFa;
                existing.Slug = model.Slug;
                existing.IsActive = model.IsActive;
                existing.SortOrder = model.SortOrder;
                existing.RequiresBoxSelection = model.RequiresBoxSelection;
                existing.ImagePath = model.ImagePath;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "دسته‌بندی با موفقیت ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound();

            bool hasSweets = await _context.SweetItems.AnyAsync(s => s.CategoryId == id);
            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasSweets || hasProducts)
            {
                TempData["Error"] = "این دسته‌بندی حاوی محصولات (شیرینی یا کالا) است. ابتدا آن‌ها را حذف یا به دسته دیگری منتقل کنید.";
                return RedirectToAction(nameof(Index));
            }
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            TempData["Success"] = "دسته‌بندی با موفقیت حذف شد.";
            return RedirectToAction(nameof(Index));
        }
    }
}
