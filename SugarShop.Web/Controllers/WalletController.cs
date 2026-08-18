using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.ViewModels;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class WalletController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WalletController(SugarShopSalesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string searchTerm)
        {
            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                usersQuery = usersQuery.Where(u => u.UserName.Contains(searchTerm) ||
                                                    u.Email.Contains(searchTerm) ||
                                                    u.FullName.Contains(searchTerm));
            }

            var users = await usersQuery.ToListAsync();
            var wallets = await _context.Wallets.ToDictionaryAsync(w => w.UserId, w => w);

            var model = users.Select(user => new UserWalletViewModel
            {
                UserId = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                Balance = wallets.ContainsKey(user.Id) ? wallets[user.Id].Balance : 0
            }).ToList();

            ViewBag.SearchTerm = searchTerm;
            return View(model);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0 };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            var transactions = await _context.WalletTransactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.User = user;
            ViewBag.Transactions = transactions;
            return View(wallet);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustBalance(string userId, decimal amount, string description)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0 };
                _context.Wallets.Add(wallet);
            }

            var settings = await GetWalletSettings();
            if (amount > 0 && settings.MaxWalletBalance > 0 && wallet.Balance + amount > settings.MaxWalletBalance)
            {
                TempData["Error"] = $"موجودی کیف پول نمی‌تواند از {settings.MaxWalletBalance:N0} تومان بیشتر شود.";
                return RedirectToAction("Details", new { userId });
            }

            wallet.Balance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = amount > 0 ? "AdminDeposit" : "AdminWithdraw",
                Description = description ?? (amount > 0 ? "افزایش موجودی توسط ادمین" : "کسر موجودی توسط ادمین"),
                CreatedAt = DateTime.UtcNow
            };
            _context.WalletTransactions.Add(transaction);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"موجودی با موفقیت {(amount > 0 ? "افزایش" : "کسر")} یافت.";
            return RedirectToAction("Details", new { userId });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Settings()
        {
            var settings = await GetWalletSettings();
            return View(settings);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(WalletSettings model)
        {
            if (ModelState.IsValid)
            {
                var settings = await _context.WalletSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new WalletSettings();
                    _context.WalletSettings.Add(settings);
                }

                settings.IsEnabled = model.IsEnabled;
                settings.ReturnType = model.ReturnType;
                settings.ReturnValue = model.ReturnValue;
                settings.MinimumOrderAmount = model.MinimumOrderAmount;
                settings.AllowDirectRecharge = model.AllowDirectRecharge;
                settings.DirectRechargeMinAmount = model.DirectRechargeMinAmount;
                settings.DirectRechargeMaxAmount = model.DirectRechargeMaxAmount;
                settings.MaxWalletBalance = model.MaxWalletBalance;
                settings.ExpiryDays = model.ExpiryDays;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "تنظیمات کیف پول ذخیره شد.";
                return RedirectToAction("Settings");
            }
            return View(model);
        }
        [Authorize]
        public async Task<IActionResult> Recharge()
        {
            var settings = await GetWalletSettings();
            if (!settings.AllowDirectRecharge)
            {
                TempData["Error"] = "شارژ مستقیم کیف پول غیرفعال است.";
                return RedirectToAction("Index", "Profile");
            }
            ViewBag.MinAmount = settings.DirectRechargeMinAmount;
            ViewBag.MaxAmount = settings.DirectRechargeMaxAmount;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recharge(decimal amount)
        {
            try
            {
                var settings = await GetWalletSettings();
                if (!settings.AllowDirectRecharge)
                {
                    TempData["Error"] = "شارژ مستقیم کیف پول غیرفعال است.";
                    return RedirectToAction("Index", "Profile");
                }

                if (amount < settings.DirectRechargeMinAmount)
                {
                    ModelState.AddModelError("", $"حداقل مبلغ شارژ {settings.DirectRechargeMinAmount:N0} تومان است.");
                    TempData["Error"] = $"حداقل مبلغ شارژ {settings.DirectRechargeMinAmount:N0} تومان است.";
                    return View();
                }

                if (settings.DirectRechargeMaxAmount > 0 && amount > settings.DirectRechargeMaxAmount)
                {
                    ModelState.AddModelError("", $"حداکثر مبلغ شارژ {settings.DirectRechargeMaxAmount:N0} تومان است.");
                    TempData["Error"] = $"حداکثر مبلغ شارژ {settings.DirectRechargeMaxAmount:N0} تومان است.";
                    return View();
                }

                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["Error"] = "کاربر یافت نشد. لطفاً مجدداً وارد شوید.";
                    return RedirectToAction("Login", "Account");
                }

                var order = new Order
                {
                    OrderCode = "WALLET-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                    UserId = userId,
                    OrderStatus = OrderStatus.PendingPayment,
                    PaymentStatus = PaymentStatus.Unpaid,
                    TotalAmountSnapshot = amount,
                    FinalTotalAmount = amount,
                    DeliveryFeeSnapshot = 0,
                    CustomerName = User.Identity?.Name ?? "کاربر مهمان",
                    CreatedAt = DateTime.UtcNow,
                    IsPaymentEnabled = true,
                    Notes = "WalletRecharge"
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                return RedirectToAction("RequestPayment", "Payment", new { orderId = order.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "خطایی رخ داد: " + ex.Message;
                return RedirectToAction("Recharge");
            }
        }
        private async Task<WalletSettings> GetWalletSettings()
        {
            var settings = await _context.WalletSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new WalletSettings();
                _context.WalletSettings.Add(settings);
                await _context.SaveChangesAsync();
            }
            return settings;
        }
    }
}