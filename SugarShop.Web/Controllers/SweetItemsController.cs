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

        // ✅ متد Index اصلاح‌شده با جستجو، فیلتر و صفحه‌بندی کامل
        public async Task<IActionResult> Index(string? search, int? categoryId, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.SweetItems
                .Include(x => x.Category)
                .AsNoTracking()
                .AsQueryable();

            // 🔍 فیلتر بر اساس نام
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s => s.TitleFa.Contains(term));
            }

            // 🗂️ فیلتر بر اساس دسته
            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(s => s.CategoryId == categoryId.Value);

            // 📊 محاسبه تعداد کل و تعداد صفحات
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            // 🔒 جلوگیری از page نامعتبر
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = await query
                .OrderByDescending(s => s.Id)   // جدیدترین‌ها اول
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 📤 ارسال داده‌ها به View
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = totalPages;     // ✅ این خط قبلاً وجود نداشت!
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            // 🗂️ لیست دسته‌ها برای dropdown فیلتر
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new { c.Id, c.TitleFa })
                .ToListAsync();

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