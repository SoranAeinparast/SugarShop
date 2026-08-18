using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,OrderManager,Owner")]
    [Route("Admin/[controller]/[action]")]
    public class ContactSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public ContactSettingsController(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync() ?? new SiteSetting();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SiteSetting model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                TempData["Error"] = "خطا در اعتبارسنجی: " + string.Join("; ", errors);
                return View(model);
            }

            bool hasValue = !string.IsNullOrEmpty(model.ContactPageTitle) ||
                !string.IsNullOrEmpty(model.ContactPageSubtitle) ||
                !string.IsNullOrEmpty(model.ContactFormTitle) ||
                !string.IsNullOrEmpty(model.ContactFormSubtitle) ||
                !string.IsNullOrEmpty(model.MapEmbedUrl) ||
                !string.IsNullOrEmpty(model.MapLatitude) ||
                !string.IsNullOrEmpty(model.MapLongitude) ||
                !string.IsNullOrEmpty(model.MapLocationName) ||
                !string.IsNullOrEmpty(model.ContactSuccessMessage);

            if (!hasValue)
            {
                TempData["Error"] = "⚠️ لطفاً حداقل یکی از فیلدها را پر کنید (مثلاً عنوان صفحه یا URL نقشه).";
                return View(model);
            }

            try
            {
                var settings = await _context.SiteSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new SiteSetting();
                    _context.SiteSettings.Add(settings);
                }

                settings.ContactPageTitle = model.ContactPageTitle;
                settings.ContactPageSubtitle = model.ContactPageSubtitle;
                settings.ContactFormTitle = model.ContactFormTitle;
                settings.ContactFormSubtitle = model.ContactFormSubtitle;
                settings.MapEmbedUrl = model.MapEmbedUrl;
                settings.MapLatitude = model.MapLatitude;
                settings.MapLongitude = model.MapLongitude;
                settings.MapLocationName = model.MapLocationName;
                settings.ContactSuccessMessage = model.ContactSuccessMessage;
                settings.UpdatedAt = DateTime.UtcNow;

                var changes = await _context.SaveChangesAsync();
                if (changes > 0)
                {
                    TempData["Success"] = "✅ تنظیمات صفحه تماس با موفقیت ذخیره شد.";
                }
                else
                {
                    TempData["Warning"] = "⚠️ هیچ تغییری اعمال نشد (مقادیر تکراری). لطفاً مقادیر جدید وارد کنید.";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ خطا در ذخیره‌سازی: {ex.Message}";
                if (ex.InnerException != null)
                    TempData["Error"] += $" | جزئیات: {ex.InnerException.Message}";
                return View(model);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,OrderManager,Owner")]
        public async Task<IActionResult> Messages()
        {
            var messages = await _context.ContactMessages
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
            return View(messages);
        }

        [HttpPost("{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message != null)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ پیام به‌عنوان خوانده‌شده علامت‌گذاری شد.";
            }
            else
            {
                TempData["Error"] = "❌ پیام یافت نشد.";
            }
            return RedirectToAction(nameof(Messages));
        }

        [HttpPost("{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message != null)
            {
                _context.ContactMessages.Remove(message);
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ پیام با موفقیت حذف شد.";
            }
            else
            {
                TempData["Error"] = "❌ پیام یافت نشد.";
            }
            return RedirectToAction(nameof(Messages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJson([FromBody] SiteSetting model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "داده ارسال نشده است" });

            try
            {
                var settings = await _context.SiteSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new SiteSetting();
                    _context.SiteSettings.Add(settings);
                }

                settings.ContactPageTitle = model.ContactPageTitle;
                settings.ContactPageSubtitle = model.ContactPageSubtitle;
                settings.ContactFormTitle = model.ContactFormTitle;
                settings.ContactFormSubtitle = model.ContactFormSubtitle;
                settings.ContactSuccessMessage = model.ContactSuccessMessage;
                settings.MapEmbedUrl = model.MapEmbedUrl;
                settings.MapLatitude = model.MapLatitude;
                settings.MapLongitude = model.MapLongitude;
                settings.MapLocationName = model.MapLocationName;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "تنظیمات صفحه تماس با موفقیت ذخیره شد." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}