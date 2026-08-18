using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers
{
    public class ContactController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public ContactController(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();
            return View(settings ?? new SiteSetting());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(ContactMessage model)
        {
            if (!ModelState.IsValid)
            {
                var settings = await _context.SiteSettings.FirstOrDefaultAsync();
                ViewBag.Settings = settings;
                return View("Index", settings);
            }

            model.CreatedAt = DateTime.UtcNow;
            model.IsRead = false;
            _context.ContactMessages.Add(model);
            await _context.SaveChangesAsync();

            TempData["ContactSuccess"] = "✅ پیام شما با موفقیت ارسال شد. در اسرع وقت با شما تماس خواهیم گرفت.";
            return RedirectToAction(nameof(Index));
        }
    }
}