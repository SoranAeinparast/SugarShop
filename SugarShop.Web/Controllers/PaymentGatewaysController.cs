using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.ViewModels;
using System.Text.Json;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class PaymentGatewaysController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly ILogger<PaymentGatewaysController> _logger;

        public PaymentGatewaysController(SugarShopSalesDbContext context, ILogger<PaymentGatewaysController> logger)
        {
            _context = context;
            _logger = logger;
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
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGateway(PaymentGateway model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("CreateGateway با مدل نامعتبر فراخوانی شد: {Errors}",
                    string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                TempData["Error"] = "اطلاعات وارد شده معتبر نیست. لطفاً خطاها را بررسی کنید.";
                return View(model);
            }

            try
            {
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;
                _context.PaymentGateways.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "درگاه جدید با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ذخیره درگاه پرداخت جدید ناموفق بود");
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
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("CreateAccount با مدل نامعتبر فراخوانی شد: {Errors}",
                    string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
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

        // ====================================================================
        // 🔑 تنظیمات درگاه زیبال — محل ثبت کلید پذیرنده (Merchant) دریافتی
        // ====================================================================
        [HttpGet]
        public async Task<IActionResult> ZibalSettings()
        {
            var model = await LoadZibalSettingsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ZibalSettings(ZibalGatewaySettingsViewModel model)
        {
            var merchantId = model.MerchantId?.Trim() ?? "";
            model.Mode = model.Mode == "Production" ? "Production" : "Test";

            if (model.IsActive && model.Mode == "Production" && string.IsNullOrWhiteSpace(merchantId))
                ModelState.AddModelError("MerchantId", "در حالت عملیاتی، وارد کردن کلید پذیرنده الزامی است.");

            if (!ModelState.IsValid)
                return View(model);

            var gateway = await _context.PaymentGateways
                .Include(g => g.Accounts)
                .FirstOrDefaultAsync(g => g.GatewayType == "Zibal");

            if (gateway == null)
            {
                gateway = new PaymentGateway
                {
                    Name = "Zibal",
                    Title = "زیبال",
                    GatewayType = "Zibal",
                    IsActive = true,
                    SortOrder = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.PaymentGateways.Add(gateway);
                await _context.SaveChangesAsync();
            }

            gateway.IsActive = true;
            gateway.UpdatedAt = DateTime.UtcNow;

            var account = gateway.Accounts?.FirstOrDefault()
                ?? new PaymentGatewayAccount
                {
                    GatewayId = gateway.Id,
                    Title = "حساب اصلی زیبال",
                    CreatedAt = DateTime.UtcNow
                };

            account.IsActive = model.IsActive;
            account.ConfigData = JsonSerializer.Serialize(new { MerchantId = merchantId, Mode = model.Mode });
            account.UpdatedAt = DateTime.UtcNow;

            if (account.Id == 0)
                _context.PaymentGatewayAccounts.Add(account);

            await _context.SaveChangesAsync();

            TempData["Success"] = model.IsActive
                ? "درگاه زیبال فعال شد. پرداخت‌ها از این پس با کلید ذخیره‌شده انجام می‌شوند."
                : "درگاه زیبال غیرفعال شد؛ تا زمانی که کلید جدید ذخیره و فعال نشود، پرداخت در حالت تست (پیش‌فرض) انجام می‌شود.";
            return RedirectToAction(nameof(ZibalSettings));
        }

        private async Task<ZibalGatewaySettingsViewModel> LoadZibalSettingsAsync()
        {
            var model = new ZibalGatewaySettingsViewModel();
            var gateway = await _context.PaymentGateways
                .Include(g => g.Accounts)
                .FirstOrDefaultAsync(g => g.GatewayType == "Zibal");

            if (gateway != null)
            {
                var account = gateway.Accounts?.FirstOrDefault();
                if (account != null)
                {
                    model.IsActive = account.IsActive;
                    if (!string.IsNullOrWhiteSpace(account.ConfigData))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(account.ConfigData);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("MerchantId", out var mid) && mid.ValueKind == JsonValueKind.String)
                                model.MerchantId = mid.GetString();
                            if (root.TryGetProperty("Mode", out var mode) && mode.ValueKind == JsonValueKind.String)
                                model.Mode = mode.GetString()!;
                        }
                        catch
                        {
                            // JSON نامعتبر - مقادیر پیش‌فرض نمایش داده می‌شوند
                        }
                    }
                }
            }
            return model;
        }
    }
}