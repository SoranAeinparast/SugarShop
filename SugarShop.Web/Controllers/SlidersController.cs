using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class SlidersController : Controller
    {
        private readonly SugarShopCatalogDbContext _context;

        public SlidersController(SugarShopCatalogDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var sliders = await _context.Sliders
                .OrderBy(s => s.SortOrder)
                .ToListAsync();
            return View(sliders);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Slider model)
        {
            if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(model.ImagePath))
                {
                    var extension = Path.GetExtension(model.ImagePath).ToLower();
                    model.IsVideo = extension == ".webm" || extension == ".mp4" || extension == ".mov";
                }

                model.CreatedAt = DateTime.UtcNow;
                _context.Sliders.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "اسلایدر با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var slider = await _context.Sliders.FindAsync(id);
            if (slider == null) return NotFound();
            return View(slider);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Slider model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var existing = await _context.Sliders.FindAsync(id);
                if (existing == null) return NotFound();

                existing.Title = model.Title;
                existing.Subtitle = model.Subtitle;
                existing.ButtonText = model.ButtonText;
                existing.ButtonUrl = model.ButtonUrl;
                existing.IsActive = model.IsActive;
                existing.SortOrder = model.SortOrder;
                existing.StartAt = model.StartAt;
                existing.EndAt = model.EndAt;
                existing.ImagePath = model.ImagePath;
                if (!string.IsNullOrEmpty(model.ImagePath))
                {
                    var extension = Path.GetExtension(model.ImagePath).ToLower();
                    existing.IsVideo = extension == ".webm" || extension == ".mp4" || extension == ".mov";
                }
                else
                {
                    existing.IsVideo = false;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "اسلایدر ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var slider = await _context.Sliders.FindAsync(id);
            if (slider != null)
            {
                _context.Sliders.Remove(slider);
                await _context.SaveChangesAsync();
                TempData["Success"] = "اسلایدر حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}