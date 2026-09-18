using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    public class SweetItemController : Controller
    {
        private readonly SugarShopCatalogDbContext _db;
        private readonly UserManager<SugarShop.Domain.Entities.ApplicationUser> _userManager;

        public SweetItemController(SugarShopCatalogDbContext db,
            UserManager<SugarShop.Domain.Entities.ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? categoryId)
        {
            var query = _db.SweetItems.AsNoTracking();

            if (categoryId.HasValue)
                query = query.Where(s => s.CategoryId == categoryId.Value);

            var items = await query
                .Include(s => s.Category)
                .OrderByDescending(s => s.SortOrder)
                .ToListAsync();

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var item = await _db.SweetItems
                .AsNoTracking()
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (item == null) return NotFound();
            return View(item);
        }

        /// <summary>«خبرم کن وقتی موجود شد» — ثبت شماره موبایل برای اطلاع‌رسانی موجودی.</summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyMe(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var item = await _db.SweetItems.FindAsync(id);
            if (item == null) return NotFound();

            // شماره از پروفایل کاربر؛ اگر ندارد، از فرم گرفته می‌شود
            var user = await _userManager.FindByIdAsync(userId);
            var phone = SugarShop.Web.Services.Sms.SmsService.NormalizePhone(
                Request.Form["phone"].ToString() is { Length: > 0 } f ? f : user?.PhoneNumber);

            if (!SugarShop.Web.Services.Sms.SmsService.IsValidIranMobile(phone))
            {
                TempData["ErrorMessage"] = "برای اطلاع از موجودی، ابتدا شماره موبایل معتبر در پروفایل خود ثبت کنید.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var db = HttpContext.RequestServices.GetRequiredService<SugarShop.Infrastructure.Persistence.Sales.SugarShopSalesDbContext>();
            bool exists = await db.RestockSubscriptions
                .AnyAsync(r => r.SweetItemId == id && r.UserId == userId && !r.Notified);
            if (!exists)
            {
                db.RestockSubscriptions.Add(new SugarShop.Domain.Entities.Sms.RestockSubscription
                {
                    SweetItemId = id,
                    UserId = userId,
                    Phone = phone,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = item.IsInStock
                ? "این محصول الان موجود است!"
                : "ثبت شد؛ به‌محض موجود شدن محصول به شما پیامک می‌دهیم. 🔔";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}