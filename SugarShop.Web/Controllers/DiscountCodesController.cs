using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class DiscountCodesController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public DiscountCodesController(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var codes = await _context.Set<DiscountCode>()
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
            return View(codes);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DiscountCode model)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Set<DiscountCode>().AnyAsync(d => d.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "این کد قبلاً ثبت شده است.");
                    return View(model);
                }

                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.Set<DiscountCode>().Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "کد تخفیف با موفقیت ایجاد شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var code = await _context.Set<DiscountCode>().FindAsync(id);
            if (code == null) return NotFound();
            return View(code);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DiscountCode model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var existing = await _context.Set<DiscountCode>().FindAsync(id);
                if (existing == null) return NotFound();
                if (await _context.Set<DiscountCode>().AnyAsync(d => d.Code == model.Code && d.Id != id))
                {
                    ModelState.AddModelError("Code", "این کد قبلاً ثبت شده است.");
                    return View(model);
                }

                existing.Code = model.Code;
                existing.Description = model.Description;
                existing.DiscountType = model.DiscountType;
                existing.DiscountValue = model.DiscountValue;
                existing.MinimumOrderAmount = model.MinimumOrderAmount;
                existing.StartDate = model.StartDate;
                existing.EndDate = model.EndDate;
                existing.UsageLimit = model.UsageLimit;
                existing.IsActive = model.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "کد تخفیف ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var code = await _context.Set<DiscountCode>().FindAsync(id);
            if (code != null)
            {
                _context.Set<DiscountCode>().Remove(code);
                await _context.SaveChangesAsync();
                TempData["Success"] = "کد تخفیف حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}