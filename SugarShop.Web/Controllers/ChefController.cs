using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
    /// <summary>
    /// داشبورد مخصوص سرآشپز: مدیریت کیک‌های سفارشی بر اساس گردش کار استاندارد
    /// در انتظار بررسی ← تأیید شده ← در حال پخت ← آماده تحویل ← تحویل شده (یا رد شده)
    /// </summary>
    [Authorize(Roles = "Chef,Admin,Owner")]
    public class ChefController : Controller
    {
        private readonly SugarShopSalesDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChefController(SugarShopSalesDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CustomCakeOrderStatus? status = null)
        {
            IQueryable<CustomCakeOrder> query = _db.CustomCakeOrders;
            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            var orders = await query
                .OrderByDescending(o => o.Status == CustomCakeOrderStatus.Pending)
                .ThenByDescending(o => o.CreatedAt)
                .ToListAsync();

            // نام مشتری‌ها در یک کوئری
            var users = await _userManager.Users.ToDictionaryAsync(u => u.Id);
            ViewBag.Users = users;
            ViewBag.SelectedStatus = status;

            // آمار داشبورد
            ViewBag.CountPending = await _db.CustomCakeOrders.CountAsync(o => o.Status == CustomCakeOrderStatus.Pending);
            ViewBag.CountAccepted = await _db.CustomCakeOrders.CountAsync(o => o.Status == CustomCakeOrderStatus.Accepted);
            ViewBag.CountInProduction = await _db.CustomCakeOrders.CountAsync(o => o.Status == CustomCakeOrderStatus.InProduction);
            ViewBag.CountReady = await _db.CustomCakeOrders.CountAsync(o => o.Status == CustomCakeOrderStatus.Ready);
            ViewBag.CountCompleted = await _db.CustomCakeOrders.CountAsync(o => o.Status == CustomCakeOrderStatus.Completed);
            ViewBag.CountRejected = await _db.CustomCakeOrders.CountAsync(o => o.Status == CustomCakeOrderStatus.Rejected);

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _db.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            ViewBag.Customer = await _userManager.FindByIdAsync(order.UserId);
            return View(order);
        }

        /// <summary>تأیید سفارش (در انتظار بررسی ← تأیید شده) + ثبت قیمت پیشنهادی و یادداشت</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, decimal? finalPrice, string? chefNotes)
        {
            var order = await _db.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != CustomCakeOrderStatus.Pending)
            {
                TempData["Error"] = "این سفارش در وضعیتی نیست که قابل تأیید باشد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = CustomCakeOrderStatus.Accepted;
            order.FinalPrice = finalPrice;
            order.AdminNotes = string.IsNullOrWhiteSpace(chefNotes) ? null : chefNotes.Trim();
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "سفارش تأیید شد و وارد دستور کار پخت گردید.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>رد سفارش (در انتظار بررسی ← رد شده) + دلیل</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            var order = await _db.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != CustomCakeOrderStatus.Pending)
            {
                TempData["Error"] = "فقط سفارش‌های در انتظار بررسی قابل رد شدن هستند.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = CustomCakeOrderStatus.Rejected;
            order.AdminNotes = string.IsNullOrWhiteSpace(reason) ? "توسط سرآشپز رد شد." : reason.Trim();
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "سفارش رد شد و به مشتری اطلاع داده می‌شود.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>شروع پخت (تأیید شده ← در حال پخت)</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartProduction(int id)
        {
            var order = await _db.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != CustomCakeOrderStatus.Accepted)
            {
                TempData["Error"] = "برای شروع پخت، ابتدا سفارش باید تأیید شده باشد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = CustomCakeOrderStatus.InProduction;
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "پخت کیک آغاز شد.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>آماده تحویل (در حال پخت ← آماده تحویل)</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReady(int id)
        {
            var order = await _db.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != CustomCakeOrderStatus.InProduction)
            {
                TempData["Error"] = "فقط سفارش‌های در حال پخت را می‌توان آماده تحویل کرد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = CustomCakeOrderStatus.Ready;
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "کیک آماده تحویل است.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>تحویل نهایی (آماده تحویل ← تحویل شده)</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var order = await _db.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != CustomCakeOrderStatus.Ready)
            {
                TempData["Error"] = "فقط سفارش‌های آماده تحویل را می‌توان تحویل نهایی کرد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = CustomCakeOrderStatus.Completed;
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "سفارش با موفقیت تحویل شد.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>دستور کار پخت — قالب چاپی زیبا برای استفاده در کارگاه</summary>
        [HttpGet]
        public async Task<IActionResult> WorkOrder(int id)
        {
            var order = await _db.CustomCakeOrders.FindAsync(id);
            if (order == null) return NotFound();

            ViewBag.Customer = await _userManager.FindByIdAsync(order.UserId);
            return View(order);
        }
    }
}