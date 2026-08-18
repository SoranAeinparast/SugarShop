using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers.Admin
{
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/[controller]")]
    public class MenuController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public MenuController(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string location = "header")
        {
            var items = await _context.MenuItems
                .Where(m => m.Location == location)
                .OrderBy(m => m.Order)
                .ToListAsync();
            ViewBag.Location = location;
            return View("~/Views/Admin/Menu/Index.cshtml", items);
        }

        [HttpGet("Create")]
        public IActionResult Create(string location)
        {
            ViewBag.Location = location;
            return View("~/Views/Admin/Menu/Create.cshtml");
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MenuItem model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.MenuItems.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "منو با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index), new { location = model.Location });
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            TempData["Error"] = string.Join(", ", errors);
            return View("~/Views/Admin/Menu/Create.cshtml", model);
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return NotFound();
            return View("~/Views/Admin/Menu/Edit.cshtml", item);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MenuItem model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var existing = await _context.MenuItems.FindAsync(id);
                if (existing == null) return NotFound();
                existing.Title = model.Title;
                existing.Url = model.Url;
                existing.ParentId = model.ParentId;
                existing.Order = model.Order;
                existing.Icon = model.Icon;
                existing.IsActive = model.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "منو با موفقیت ویرایش شد.";
                return RedirectToAction(nameof(Index), new { location = existing.Location });
            }
            return View("~/Views/Admin/Menu/Edit.cshtml", model);
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item != null)
            {
                _context.MenuItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "منو با موفقیت حذف شد.";
            }
            return RedirectToAction(nameof(Index), new { location = item?.Location ?? "header" });
        }
    }
}