using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using System.Text.Json;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class PaymentGatewaysController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public PaymentGatewaysController(SugarShopSalesDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var gateways = await _context.PaymentGateways
                .Include(g => g.Accounts)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();
            return View(gateways);
        }
        [HttpGet]
        public async Task<IActionResult> EditAccount(int id)
        {
            var account = await _context.PaymentGatewayAccounts
                .Include(a => a.Gateway)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (account == null) return NotFound();
            return View(account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(int id, PaymentGatewayAccount model, string configJson)
        {
            if (id != model.Id) return NotFound();

            var account = await _context.PaymentGatewayAccounts.FindAsync(id);
            if (account == null) return NotFound();

            account.Title = model.Title;
            account.IsActive = model.IsActive;
            account.ConfigData = configJson;
            account.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "تنظیمات درگاه با موفقیت ذخیره شد.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.PaymentGatewayAccounts.Any(a => a.Id == id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> ToggleGateway(int id, bool isActive)
        {
            var gateway = await _context.PaymentGateways.FindAsync(id);
            if (gateway == null) return NotFound();

            gateway.IsActive = isActive;
            gateway.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"درگاه {(isActive ? "فعال" : "غیرفعال")} شد.";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var account = await _context.PaymentGatewayAccounts.FindAsync(id);
            if (account != null)
            {
                _context.PaymentGatewayAccounts.Remove(account);
                await _context.SaveChangesAsync();
                TempData["Success"] = "حساب درگاه حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public IActionResult CreateGateway()
        {
            return View(new PaymentGateway { IsActive = true, SortOrder = 100 });
        }

        [HttpPost]
        public async Task<IActionResult> CreateGateway(PaymentGateway model)
        {
            Console.WriteLine("=== CreateGateway called ===");
            Console.WriteLine($"Model: Name={model.Name}, Title={model.Title}, GatewayType={model.GatewayType}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is invalid!");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                }
                TempData["Error"] = "اطلاعات وارد شده معتبر نیست. لطفاً خطاها را بررسی کنید.";
                return View(model);
            }

            try
            {
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.PaymentGateways.Add(model);
                await _context.SaveChangesAsync();
                Console.WriteLine("Gateway saved successfully!");
                TempData["Success"] = "درگاه جدید با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                TempData["Error"] = $"خطا در ذخیره‌سازی: {ex.Message}";
                return View(model);
            }
        }
        [HttpGet]
        public async Task<IActionResult> CreateAccount(int gatewayId)
        {
            var gateway = await _context.PaymentGateways.FindAsync(gatewayId);
            if (gateway == null) return NotFound();
            ViewBag.Gateway = gateway;
            return View(new PaymentGatewayAccount { GatewayId = gatewayId, IsActive = true, ConfigData = "{}" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(PaymentGatewayAccount model, string configJson)
        {
            Console.WriteLine("CreateAccount called");
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Validation error: {error.ErrorMessage}");
                }
                var gateway = await _context.PaymentGateways.FindAsync(model.GatewayId);
                ViewBag.Gateway = gateway;
                return View(model);
            }

            model.ConfigData = configJson;
            model.CreatedAt = DateTime.UtcNow;
            _context.PaymentGatewayAccounts.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "حساب جدید با موفقیت اضافه شد.";
            return RedirectToAction(nameof(Index));
        }
    }
}