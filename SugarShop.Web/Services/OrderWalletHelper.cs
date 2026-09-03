using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// کمکی برای بازگشت وجه کیف پول هنگام حذف/ابطال سفارش.
    /// مبلغ کسرشده از کیف پول در تراکنش‌های Type=Purchase با Amount منفی ثبت شده است.
    /// </summary>
    public static class OrderWalletHelper
    {
        /// <summary>مبلغی که هنگام ثبت سفارش از کیف پول کسر شده است.</summary>
        public static async Task<decimal> GetWalletUsedAsync(SugarShopSalesDbContext db, int orderId)
        {
            return await db.WalletTransactions
                .Where(t => t.OrderId == orderId && t.Type == "Purchase" && t.Amount < 0)
                .SumAsync(t => (decimal?)(-t.Amount)) ?? 0m;
        }

        /// <summary>
        /// بازگشت وجه کیف پول هنگام حذف سفارش (idempotent؛ فقط یک‌بار انجام می‌شود).
        /// این متد ذخیره نمی‌کند؛ فراخوان آن‌ها را در تراکنش خود ذخیره می‌کند.
        /// </summary>
        public static async Task RefundWalletAsync(SugarShopSalesDbContext db, Order order)
        {
            var walletUsed = await GetWalletUsedAsync(db, order.Id);
            if (walletUsed <= 0) return;

            // جلوگیری از بازگشت تکراری
            var alreadyRefunded = await db.WalletTransactions
                .AnyAsync(t => t.OrderId == order.Id && t.Type == "OrderRefund");
            if (alreadyRefunded) return;

            if (string.IsNullOrEmpty(order.UserId)) return;

            var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == order.UserId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = order.UserId, Balance = 0 };
                db.Wallets.Add(wallet);
            }

            wallet.Balance += walletUsed;
            wallet.UpdatedAt = System.DateTime.UtcNow;

            db.WalletTransactions.Add(new WalletTransaction
            {
                UserId = order.UserId,
                Amount = walletUsed,
                Type = "OrderRefund",
                Description = $"بازگشت وجه کیف پول به دلیل حذف سفارش {order.OrderCode}",
                OrderId = order.Id,
                CreatedAt = System.DateTime.UtcNow
            });
        }
    }
}
