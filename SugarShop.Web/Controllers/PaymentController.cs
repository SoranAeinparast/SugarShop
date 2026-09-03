using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Infrastructure.Services;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ZibalPaymentService _zibalPaymentService;
        private readonly SugarShopSalesDbContext _context;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly InventoryService _inventoryService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(SugarShopSalesDbContext context,
                                 SugarShopCatalogDbContext catalogDb,
                                 UserManager<ApplicationUser> userManager,
                                 IConfiguration configuration,
                                 ZibalPaymentService zibalPaymentService,
                                 InventoryService inventoryService,
                                 ILogger<PaymentController> logger)
        {
            _context = context;
            _catalogDb = catalogDb;
            _userManager = userManager;
            _configuration = configuration;
            _zibalPaymentService = zibalPaymentService;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> RequestPayment(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return NotFound();

            bool isWalletRecharge = order.Notes == "WalletRecharge";
            if (isWalletRecharge)
            {
                if (order.PaymentStatus != PaymentStatus.Unpaid)
                    return BadRequest("پرداخت برای این سفارش امکان‌پذیر نیست.");
            }
            else
            {
                if (!order.IsPaymentEnabled || order.PaymentStatus != PaymentStatus.Unpaid)
                    return BadRequest("پرداخت برای این سفارش امکان‌پذیر نیست.");
            }

            // ✅ اصلاح: محاسبه دقیق مبلغ شامل همه اقلام + هزینه پیک
            long amountInTomans = (long)((order.FinalTotalAmount ?? order.TotalAmountSnapshot) + order.DeliveryFeeSnapshot);
            long amountInRials = amountInTomans * 10;

            string callbackUrl = Url.Action("Callback", "Payment", new { orderId = order.Id }, Request.Scheme);
            string description = isWalletRecharge ? "شارژ کیف پول" : $"پرداخت سفارش {order.OrderCode}";

            var zibalMerchant = await ResolveActiveZibalMerchantAsync();
            var result = await _zibalPaymentService.RequestPayment(amountInRials, description, callbackUrl, merchant: zibalMerchant);

            if (result.Success)
            {
                var payment = new Payment
                {
                    OrderId = order.Id,
                    Authority = result.TrackId,
                    Amount = amountInTomans,  // ✅ مبلغ صحیح شامل همه چیز
                    Provider = "Zibal",
                    PaymentStatus = PaymentStatus.Initiated,
                    CreatedAt = DateTime.UtcNow,
                    TransactionCode = result.TrackId
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
                return Redirect(result.PaymentUrl);
            }
            else
            {
                TempData["Error"] = result.ErrorMessage;
                if (isWalletRecharge)
                    return RedirectToAction("Recharge", "Wallet");
                else
                    return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }
        }
        [HttpGet]
        public async Task<IActionResult> Callback(int orderId, string trackId, string success, int status)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Authority == trackId);
            if (payment == null) return NotFound();

            if (payment.PaymentStatus == PaymentStatus.Succeeded)
            {
                TempData["Info"] = "این پرداخت قبلاً پردازش شده است.";
                return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            bool isWalletRecharge = order.Notes == "WalletRecharge";

            if (success != "1" || status != 2)
            {
                payment.PaymentStatus = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["Error"] = "پرداخت توسط کاربر لغو شد یا ناموفق بود.";
                return isWalletRecharge ? RedirectToAction("Recharge", "Wallet") : RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            var zibalMerchant = await ResolveActiveZibalMerchantAsync();
            var verifyResult = await _zibalPaymentService.VerifyPayment(trackId, zibalMerchant);
            if (!verifyResult.Success)
            {
                payment.PaymentStatus = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["Error"] = verifyResult.ErrorMessage;
                return isWalletRecharge ? RedirectToAction("Recharge", "Wallet") : RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            // ✅ اصلاح: بررسی مبلغ شامل هزینه پیک
            long expectedAmount = (long)((order.FinalTotalAmount ?? order.TotalAmountSnapshot) + order.DeliveryFeeSnapshot);
            if (payment.Amount != expectedAmount)
            {
                _logger.LogError("Amount mismatch for order {OrderId}. Expected: {Expected}, Payment: {Actual}", order.Id, expectedAmount, payment.Amount);
                payment.PaymentStatus = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["Error"] = "مغایرت در مبلغ پرداخت. لطفاً با پشتیبانی تماس بگیرید.";
                return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            // ===== پردازش نهایی اتمیک و idempotent =====
            // وضعیت پرداخت فقط به‌صورت شرطی از Initiated به Succeeded تغییر می‌کند؛
            // اگر دو درخواست همزمان برسند، فقط یکی موفق می‌شود (جلوگیری از شارژ/کسر مضاعف).
            // کسر موجودی انبار و شارژ کیف پول هم در همان تراکنش انجام می‌شود تا ناسازگاری ایجاد نشود.
            using var tx = await _context.Database.BeginTransactionAsync();
            _catalogDb.Database.UseTransaction(tx.GetDbTransaction());

            try
            {
                var claimed = await _context.Payments
                    .Where(p => p.Id == payment.Id && p.PaymentStatus == PaymentStatus.Initiated)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.PaymentStatus, PaymentStatus.Succeeded)
                        .SetProperty(p => p.TransactionCode, verifyResult.RefNumber.ToString()));

                if (claimed == 0)
                {
                    // درخواست دیگری همین پرداخت را قبلاً پردازش کرده است
                    await tx.RollbackAsync();
                    TempData["Info"] = "این پرداخت قبلاً پردازش شده است.";
                    return isWalletRecharge
                        ? RedirectToAction("Wallet", "Profile")
                        : RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
                }

                order.PaymentStatus = PaymentStatus.Succeeded;
                order.OrderStatus = OrderStatus.Paid;
                order.IsPaymentEnabled = false;  // ✅ جلوگیری از پرداخت مجدد
                await _context.SaveChangesAsync();

                // کسر موجودی انبار (هر دو DbContext به یک دیتابیس وصل‌اند؛ پس داخل همین تراکنش است)
                if (!isWalletRecharge)
                    await _inventoryService.DecreaseInventoryAsync(order);

                if (order.Notes != null && order.Notes.StartsWith("CustomCakeOrder_"))
                {
                    var cakeOrderId = int.Parse(order.Notes.Split('_')[1]);
                    var cakeOrder = await _context.CustomCakeOrders.FindAsync(cakeOrderId);
                    if (cakeOrder != null)
                    {
                        cakeOrder.IsPaid = true;
                        cakeOrder.UpdatedAt = DateTime.UtcNow;
                    }
                }

                if (isWalletRecharge)
                {
                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == order.UserId);
                    if (wallet == null)
                    {
                        wallet = new Wallet { UserId = order.UserId, Balance = 0 };
                        _context.Wallets.Add(wallet);
                    }
                    wallet.Balance += order.TotalAmountSnapshot;
                    wallet.UpdatedAt = DateTime.UtcNow;
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = order.UserId,
                        Amount = order.TotalAmountSnapshot,
                        Type = "DirectRecharge",
                        Description = "شارژ مستقیم کیف پول از طریق درگاه زیبال",
                        OrderId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در پردازش نهایی پرداخت سفارش {OrderId}؛ تراکنش برگشت داده شد", order.Id);
                TempData["Error"] = "خطا در ثبت پرداخت. اگر مبلغی کسر شده است، با پشتیبانی تماس بگیرید.";
                return isWalletRecharge
                    ? RedirectToAction("Wallet", "Profile")
                    : RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            if (order.Notes != null && order.Notes.StartsWith("CustomCakeOrder_"))
            {
                TempData["Success"] = $"پرداخت با موفقیت انجام شد. شماره پیگیری: {verifyResult.RefNumber}";
                return RedirectToAction("CustomCakeOrders", "Profile");
            }

            if (isWalletRecharge)
            {
                TempData["Success"] = $"کیف پول شما با موفقیت شارژ شد. شماره پیگیری: {verifyResult.RefNumber}";
                return RedirectToAction("Wallet", "Profile");
            }

            TempData["Success"] = $"پرداخت با موفقیت انجام شد. شماره پیگیری: {verifyResult.RefNumber}";
            return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
        }

        /// <summary>
        /// خواندن کلید پذیرنده زیبال از تنظیمات دیتابیس (حساب فعال درگاه زیبال).
        /// اگر هنوز کلیدی ذخیره نشده باشد، مقدار appsettings (پیش‌فرض تست) استفاده می‌شود.
        /// </summary>
        private async Task<string?> ResolveActiveZibalMerchantAsync()
        {
            var merchant = _configuration["Zibal:Merchant"];

            try
            {
                var gateway = await _context.PaymentGateways
                    .Include(g => g.Accounts)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.GatewayType == "Zibal" && g.IsActive);

                var account = gateway?.Accounts?.FirstOrDefault(a => a.IsActive && !string.IsNullOrWhiteSpace(a.ConfigData));
                if (account != null)
                {
                    using var doc = JsonDocument.Parse(account.ConfigData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("MerchantId", out var mid) && mid.ValueKind == JsonValueKind.String)
                    {
                        var stored = mid.GetString();
                        if (!string.IsNullOrWhiteSpace(stored))
                            merchant = stored.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "عدم موفقیت در خواندن تنظیمات درگاه زیبال از دیتابیس؛ استفاده از مقدار پیش‌فرض");
            }

            return merchant;
        }
    }
}
