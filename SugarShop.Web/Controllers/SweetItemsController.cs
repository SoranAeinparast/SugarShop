using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class SweetItemsController : Controller
    {
        private readonly SugarShopCatalogDbContext _context;

        public SweetItemsController(SugarShopCatalogDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var query = _context.SweetItems
                .Include(x => x.Category)
                .OrderBy(x => x.SortOrder)
                .AsNoTracking();

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalCount = totalCount;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;

            return View(items);
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
        public async Task<IActionResult> Create(SweetItem model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.SweetItems.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "شیرینی با موفقیت اضافه شد.";
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
            var item = await _context.SweetItems.FindAsync(id);
            if (item == null)
                return NotFound();
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            return View(item);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SweetItem model)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _context.SweetItems.FindAsync(id);
                if (existing == null)
                    return NotFound();
                existing.TitleFa = model.TitleFa;
                existing.Slug = model.Slug;
                existing.CategoryId = model.CategoryId;
                existing.ApproxWeightGrams = model.ApproxWeightGrams;
                existing.PricePerKg = model.PricePerKg;
                existing.InventoryCount = model.InventoryCount;
                existing.IsActive = model.IsActive;
                existing.SortOrder = model.SortOrder;
                existing.Description = model.Description;
                existing.ImagePath = model.ImagePath;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "شیرینی با موفقیت ویرایش شد.";
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
            var item = await _context.SweetItems.FindAsync(id);
            if (item != null)
            {
                _context.SweetItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "شیرینی حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}