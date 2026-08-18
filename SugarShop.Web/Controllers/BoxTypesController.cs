using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class BoxTypesController : Controller
    {
        private readonly SugarShopCatalogDbContext _context;

        public BoxTypesController(SugarShopCatalogDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var boxTypes = await _context.BoxTypes
                .OrderBy(b => b.SortOrder)
                .ToListAsync();
            return View(boxTypes);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BoxType model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.BoxTypes.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "جعبه با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var box = await _context.BoxTypes.FindAsync(id);
            if (box == null) return NotFound();
            return View(box);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BoxType model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var existing = await _context.BoxTypes.FindAsync(id);
                if (existing == null) return NotFound();

                existing.TitleFa = model.TitleFa;
                existing.CapacityGrams = model.CapacityGrams;
                existing.MaxRows = model.MaxRows;
                existing.IsActive = model.IsActive;
                existing.SortOrder = model.SortOrder;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "جعبه ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var box = await _context.BoxTypes.FindAsync(id);
            if (box != null)
            {
                _context.BoxTypes.Remove(box);
                await _context.SaveChangesAsync();
                TempData["Success"] = "جعبه حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}