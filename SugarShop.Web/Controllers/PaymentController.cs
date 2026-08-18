using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            long amountInTomans = (long)(order.FinalTotalAmount ?? order.TotalAmountSnapshot);
            long amountInRials = amountInTomans * 10;
            string callbackUrl = Url.Action("Callback", "Payment", new { orderId = order.Id }, Request.Scheme);
            string description = isWalletRecharge ? "شارژ کیف پول" : $"پرداخت سفارش {order.OrderCode}";

            var result = await _zibalPaymentService.RequestPayment(amountInRials, description, callbackUrl);

            if (result.Success)
            {
                var payment = new Payment
                {
                    OrderId = order.Id,
                    Authority = result.TrackId,
                    Amount = amountInTomans,
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

            // Idempotency: if payment already succeeded, redirect without reprocessing
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

            var verifyResult = await _zibalPaymentService.VerifyPayment(trackId);
            if (!verifyResult.Success)
            {
                payment.PaymentStatus = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["Error"] = verifyResult.ErrorMessage;
                return isWalletRecharge ? RedirectToAction("Recharge", "Wallet") : RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            // Verify the amount matches the order
            long expectedAmount = (long)(order.FinalTotalAmount ?? order.TotalAmountSnapshot);
            if (payment.Amount != expectedAmount)
            {
                _logger.LogError("Amount mismatch for order {OrderId}. Expected: {Expected}, Payment: {Actual}", order.Id, expectedAmount, payment.Amount);
                payment.PaymentStatus = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["Error"] = "مغایرت در مبلغ پرداخت. لطفاً با پشتیبانی تماس بگیرید.";
                return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
            }

            payment.PaymentStatus = PaymentStatus.Succeeded;
            payment.TransactionCode = verifyResult.RefNumber.ToString();
            order.PaymentStatus = PaymentStatus.Succeeded;
            order.OrderStatus = OrderStatus.Paid;
            await _context.SaveChangesAsync();

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
                    await _context.SaveChangesAsync();
                }
                TempData["Success"] = $"پرداخت با موفقیت انجام شد. شماره پیگیری: {verifyResult.RefNumber}";
                return RedirectToAction("CustomCakeOrders", "Profile");
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

                var transaction = new WalletTransaction
                {
                    UserId = order.UserId,
                    Amount = order.TotalAmountSnapshot,
                    Type = "DirectRecharge",
                    Description = "شارژ مستقیم کیف پول از طریق درگاه زیبال",
                    OrderId = order.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.WalletTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"کیف پول شما با موفقیت شارژ شد. شماره پیگیری: {verifyResult.RefNumber}";
                return RedirectToAction("Wallet", "Profile");
            }

            TempData["Success"] = $"پرداخت با موفقیت انجام شد. شماره پیگیری: {verifyResult.RefNumber}";
            return RedirectToAction("OrderDetails", "Profile", new { id = order.Id });
        }
    }
}
