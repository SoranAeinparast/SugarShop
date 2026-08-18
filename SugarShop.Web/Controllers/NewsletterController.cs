using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Newsletter;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers
{
    public class NewsletterController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public NewsletterController(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("ایمیل الزامی است.");

            var existing = await _context.Subscribers.FirstOrDefaultAsync(s => s.Email == email);
            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "Home", new { subscribed = "active" });
                }
                else
                {
                    return RedirectToAction("Index", "Home", new { subscribed = "exists" });
                }
            }
            else
            {
                var subscriber = new Subscriber { Email = email, IsActive = true, CreatedAt = DateTime.UtcNow };
                _context.Subscribers.Add(subscriber);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home", new { subscribed = "success" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Unsubscribe(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest();

            var subscriber = await _context.Subscribers.FirstOrDefaultAsync(s => s.UnsubscribeToken == token);
            if (subscriber == null)
                return NotFound();

            subscriber.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = "اشتراک شما لغو شد.";
            return RedirectToAction("Index", "Home");
        }
    }
}
