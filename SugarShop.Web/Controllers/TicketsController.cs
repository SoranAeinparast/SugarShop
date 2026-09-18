using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,OrderManager,Owner")]
    public class TicketsController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SugarShop.Web.Services.Sms.SmsService _sms;

        public TicketsController(SugarShopSalesDbContext context, UserManager<ApplicationUser> userManager,
            SugarShop.Web.Services.Sms.SmsService sms)
        {
            _context = context;
            _userManager = userManager;
            _sms = sms;
        }
        public async Task<IActionResult> Index(string status = "all")
        {
            var query = _context.Tickets.AsQueryable();

            if (status == "open")
                query = query.Where(t => t.Status == TicketStatus.Open);
            else if (status == "answered")
                query = query.Where(t => t.Status == TicketStatus.Answered);
            else if (status == "closed")
                query = query.Where(t => t.Status == TicketStatus.Closed);

            var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            var userIds = tickets.Select(t => t.UserId).Distinct().ToList();
            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName);
            ViewBag.UserNames = users;
            ViewBag.CurrentStatus = status;
            return View(tickets);
        }
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();
            var user = await _userManager.FindByIdAsync(ticket.UserId);
            ViewBag.UserName = user?.UserName ?? "نامشخص";
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, string adminResponse)
        {
            if (string.IsNullOrWhiteSpace(adminResponse))
            {
                TempData["Error"] = "لطفاً متن پاسخ را وارد کنید.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.AdminResponse = adminResponse.Trim();
            ticket.Status = TicketStatus.Answered;
            ticket.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                // ── پیامک «تیکت شما پاسخ داده شد» ──
                try { await _sms.NotifyTicketAnsweredAsync(ticket.UserId, ticket.Id); }
                catch { /* پیامک نباید پاسخ‌دهی را متوقف کند */ }
                TempData["Success"] = "پاسخ با موفقیت ثبت شد.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در ذخیره پاسخ: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, TicketStatus status)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.Status = status;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = "وضعیت پیام تغییر کرد.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}