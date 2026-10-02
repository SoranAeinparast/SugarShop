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

        // ✅ متد Index اصلاح‌شده با جستجو، فیلتر و صفحه‌بندی کامل
        public async Task<IActionResult> Index(string? search, int? categoryId, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .AsQueryable();

            // 🔍 فیلتر بر اساس نام
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.TitleFa.Contains(term));
            }

            // 🗂️ فیلتر بر اساس دسته
            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            // 📊 محاسبه تعداد کل و تعداد صفحات
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            // 🔒 جلوگیری از page نامعتبر
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var products = await query
                .OrderBy(p => p.SortOrder)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 📤 ارسال داده‌ها به View
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = totalPages;
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

            return View(products);
        }

        // ⬇️ بقیه متدها کاملاً بدون تغییر باقی می‌مانند ⬇️

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