using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities.Sms;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services.Sms
{
    /// <summary>آیتم صف ارسال پیامک.</summary>
    public record SmsQueueItem(
        string Phone,
        string Message,
        SmsScenario Scenario,
        SmsRecipientType RecipientType = SmsRecipientType.Customer,
        string? RefKey = null);

    /// <summary>
    /// صف پیامک ماندگار (Outbox در دیتابیس): هر پیامک قبل از ارسال در جدول SmsOutboxItems
    /// ثبت می‌شود؛ در نتیجه با ری‌استارت شدن سایت هیچ پیامکی گم نمی‌شود و صف ادامه پیدا می‌کند.
    /// درخواست‌های HTTP هرگز منتظر ارسال واقعی نمی‌مانند — فقط یک INSERT سریع انجام می‌شود.
    /// </summary>
    public interface ISmsQueue
    {
        ValueTask EnqueueAsync(SmsQueueItem item);
        int ApproximateCount { get; }
    }

    public class DbSmsQueue : ISmsQueue
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public DbSmsQueue(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async ValueTask EnqueueAsync(SmsQueueItem item)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
            db.SmsOutboxItems.Add(new SmsOutboxItem
            {
                Phone = item.Phone,
                Message = item.Message,
                Scenario = item.Scenario,
                RecipientType = item.RecipientType,
                RefKey = item.RefKey,
                Status = SmsSendStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public int ApproximateCount
        {
            get
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
                    return db.SmsOutboxItems.Count(x => x.Status == SmsSendStatus.Pending);
                }
                catch { return 0; }
            }
        }
    }

    /// <summary>
    /// پردازنده پس‌زمینه صف پیامک: از جدول Outbox با قفل اتمیک (CLAIM) برداشت می‌کند،
    /// ارسال می‌کند و در صورت خطا تا ۳ بار با فاصله زمانی دوباره تلاش می‌کند.
    /// آیتم‌های گیرکرده (Processing نیمه‌کاره پس از کرش) پس از ۱۰ دقیقه آزاد می‌شوند.
    /// </summary>
    public class SmsProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmsProcessor> _logger;
        private static int _delaySeconds = 2;
        private static int _batchSize = 20;
        private const int MaxAttempts = 3;

        public SmsProcessor(IServiceScopeFactory scopeFactory, ILogger<SmsProcessor> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>پنل مدیریت تنظیمات صف را به‌روزرسانی می‌کند.</summary>
        public static void UpdateThrottle(int batchSize, int delaySeconds)
        {
            if (batchSize > 0) _batchSize = Math.Min(batchSize, 200);
            if (delaySeconds >= 0) _delaySeconds = Math.Min(delaySeconds, 60);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SMS outbox processor started (DB-backed).");
            var staleCutoff = DateTime.UtcNow.AddMinutes(-10);

            // بازیابی آیتم‌های نیمه‌کارهٔ اجرای قبلی (کرش/ری‌استارت)
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
                await db.SmsOutboxItems
                    .Where(x => x.Status == SmsSendStatus.Processing)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SmsSendStatus.Pending));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS outbox: could not reset stale processing items at startup.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var batch = ClaimBatchAsync(staleCutoff);
                    if (batch.Count == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _delaySeconds)), stoppingToken);
                        continue;
                    }

                    foreach (var item in batch)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        await SendOneAsync(item);
                        // فاصله بین ارسال‌ها فقط وقتی بیش از یک پیامک در دسته هست (ضد اسپم‌بلاک خط)
                        if (_delaySeconds > 0 && batch.Count > 1)
                            await Task.Delay(TimeSpan.FromSeconds(_delaySeconds), stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SMS outbox loop error.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            _logger.LogInformation("SMS outbox processor stopped.");
        }

        private List<SmsOutboxItem> ClaimBatchAsync(DateTime staleCutoff)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

            // قفل اتمیک: چند نمونه سایت هم‌زمان روی یک دیتابیس، پیامک تکراری نمی‌فرستند
            var ids = db.SmsOutboxItems
                .Where(x => x.Status == SmsSendStatus.Pending
                    || (x.Status == SmsSendStatus.Processing && x.CreatedAt < staleCutoff))
                .OrderBy(x => x.Id)
                .Take(_batchSize)
                .Select(x => x.Id)
                .ToList();

            if (ids.Count == 0) return new List<SmsOutboxItem>();

            var now = DateTime.UtcNow;
            var claimed = new List<SmsOutboxItem>();
            foreach (var id in ids)
            {
                // UPDATE شرطی = قفل بهینه‌ستیک؛ فقط همین پردازنده مالکیت این آیتم را می‌گیرد
                var ok = db.SmsOutboxItems
                    .Where(x => x.Id == id && x.Status == SmsSendStatus.Pending)
                    .ExecuteUpdate(s => s
                        .SetProperty(x => x.Status, SmsSendStatus.Processing)
                        .SetProperty(x => x.CreatedAt, now));
                if (ok == 1)
                {
                    var item = db.SmsOutboxItems.AsNoTracking().First(x => x.Id == id);
                    // ترتیب فیلدها در عبارت EF مهم نیست؛ فقط خواندن است
                    claimed.Add(item);
                }
            }
            return claimed;
        }

        private async Task SendOneAsync(SmsOutboxItem item)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<SmsService>();
                await sender.SendNowAsync(item.Phone, item.Message, item.Scenario, item.RecipientType, item.RefKey);

                using var doneScope = _scopeFactory.CreateScope();
                var db = doneScope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
                await db.SmsOutboxItems
                    .Where(x => x.Id == item.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, SmsSendStatus.Sent)
                        .SetProperty(x => x.SentAt, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS outbox: send failed for item {Id} to {Phone}", item.Id, item.Phone);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
                    var attempts = item.Attempts + 1;
                    var errText = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
                    if (attempts >= MaxAttempts)
                    {
                        await db.SmsOutboxItems.Where(x => x.Id == item.Id).ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.Status, SmsSendStatus.Failed)
                            .SetProperty(x => x.Attempts, attempts)
                            .SetProperty(x => x.ErrorMessage, errText));
                    }
                    else
                    {
                        // تلاش مجدد بعدی: دوباره Pending می‌شود
                        await db.SmsOutboxItems.Where(x => x.Id == item.Id).ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.Status, SmsSendStatus.Pending)
                            .SetProperty(x => x.Attempts, attempts)
                            .SetProperty(x => x.ErrorMessage, errText));
                    }
                }
                catch (Exception inner)
                {
                    _logger.LogError(inner, "SMS outbox: failed to record failure for item {Id}", item.Id);
                }
            }
        }
    }
}
