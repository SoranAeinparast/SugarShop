using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// ثبت اثر پیامک‌های لینک‌دار (ارسال و بازدید) برای پنل مدیریت.
    ///
    /// همه‌ی متدها «بهترین تلاش» هستند: خطای ثبت آمار هرگز نباید ارسال پیامک،
    /// ورود مشتری یا باز شدن لینک پرداخت را خراب کند؛ پس خطا فقط در لاگ می‌ماند.
    /// </summary>
    public class SmsLinkTrackingService
    {
        private readonly SugarShopSalesDbContext _db;
        private readonly ILogger<SmsLinkTrackingService> _logger;

        public SmsLinkTrackingService(SugarShopSalesDbContext db, ILogger<SmsLinkTrackingService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>پیامک حاوی لینک به صف ارسال سپرده شد (اولین بار برای هر سفارش/نوع ثبت می‌شود).</summary>
        public Task MarkSentAsync(int orderId, SmsLinkKind kind) => UpsertAsync(orderId, kind, isOpen: false);

        /// <summary>لینک پیامکی باز شد — هم مبنای «نرخ باز شدن» و هم «تعداد بازدید».</summary>
        public Task RegisterOpenAsync(int orderId, SmsLinkKind kind) => UpsertAsync(orderId, kind, isOpen: true);

        private async Task UpsertAsync(int orderId, SmsLinkKind kind, bool isOpen)
        {
            if (orderId <= 0) return;

            try
            {
                var now = DateTime.UtcNow;
                var row = await _db.SmsLinkTrackings
                    .FirstOrDefaultAsync(t => t.OrderId == orderId && t.Kind == kind);

                if (row == null)
                {
                    row = new SmsLinkTracking { OrderId = orderId, Kind = kind };
                    _db.SmsLinkTrackings.Add(row);
                }

                if (isOpen)
                {
                    row.FirstOpenedAt ??= now;
                    row.LastOpenedAt = now;
                    row.OpenCount += 1;
                }
                else
                {
                    row.SentAt ??= now;
                }

                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // احتمال رقابت: دو بازدید هم‌زمان، هر دو ردیف تازه ساخته‌اند و یکی به ایندکس یکتا خورده است.
                // یک بار دیگر فقط ردیف موجود را به‌روز می‌کنیم.
                try
                {
                    var now = DateTime.UtcNow;
                    var existing = await _db.SmsLinkTrackings
                        .FirstOrDefaultAsync(t => t.OrderId == orderId && t.Kind == kind);
                    if (existing != null)
                    {
                        if (isOpen)
                        {
                            existing.FirstOpenedAt ??= now;
                            existing.LastOpenedAt = now;
                            existing.OpenCount += 1;
                        }
                        else
                        {
                            existing.SentAt ??= now;
                        }
                        await _db.SaveChangesAsync();
                    }
                }
                catch (Exception retryEx)
                {
                    _logger.LogWarning(retryEx, "ثبت آمار بازدید لینک سفارش {OrderId} ناموفق بود.", orderId);
                }

                _logger.LogDebug(ex, "رقابت در ثبت آمار لینک سفارش {OrderId} (داده گزارش تقریبی است).", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ثبت آمار لینک پیامکی سفارش {OrderId} ناموفق بود.", orderId);
            }
        }
    }
}
