using Microsoft.EntityFrameworkCore;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// کارهای دوره‌ای نگهداری دیتابیس (هر شب): جدول‌هایی که با گذشت زمان بی‌دلیل بزرگ می‌شوند
    /// پاک‌سازی می‌شوند — توکن‌های منقضی صورت‌حساب، لاگ پیامک‌های قدیمی، صف پیامکِ تمام‌شده،
    /// کدهای یکبارمصرف کهنه، لاگ یادآوری‌های قدیمی، اشتراک‌های «موجود شد»ی که پیامکشان رفته
    /// و ردیف‌های منقضی سشن.
    /// هر بخش مستقل و در try/catch خودش اجرا می‌شود؛ خرابی یک بخش بقیه را متوقف نمی‌کند.
    /// بازه‌های نگهداری از appsettings (بخش Maintenance) خوانده می‌شوند و پیش‌فرض‌ها محافظه‌کارانه‌اند.
    /// </summary>
    public class MaintenanceJobs
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MaintenanceJobs> _logger;

        public MaintenanceJobs(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MaintenanceJobs> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// پاک‌سازی شبانه. هیچ‌وقت داده‌ی تازه یا داده‌ی در جریان را حذف نمی‌کند:
        /// فقط ردیف‌هایی که کارشان تمام شده یا منقضی شده‌اند و از بازه نگهداری گذشته‌اند.
        /// </summary>
        [Hangfire.DisableConcurrentExecution(timeoutInSeconds: 30 * 60)]
        public async Task RunNightlyCleanupAsync()
        {
            var now = DateTime.UtcNow;

            var smsLogDays = RetentionDays("Maintenance:SmsLogRetentionDays", 365);
            var outboxDays = RetentionDays("Maintenance:SmsOutboxRetentionDays", 30);
            var otpDays = RetentionDays("Maintenance:SmsOtpRetentionDays", 7);
            var reminderLogDays = RetentionDays("Maintenance:ReminderLogRetentionDays", 180);
            var restockDays = RetentionDays("Maintenance:RestockSubscriptionRetentionDays", 180);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

            // قواعد پاک‌سازی در CleanupRules است (قابل آزمودن بدون دیتابیس)
            var links = await TryDeleteAsync("StatementLinks", () =>
                db.StatementLinks.Where(CleanupRules.ExpiredStatementLinks(now)).ExecuteDeleteAsync());

            var smsLogs = await TryDeleteAsync("SmsLogs", () =>
                db.SmsLogs.Where(CleanupRules.OldSmsLogs(now, smsLogDays)).ExecuteDeleteAsync());

            var outbox = await TryDeleteAsync("SmsOutboxItems", () =>
                db.SmsOutboxItems.Where(CleanupRules.FinishedOutboxItems(now, outboxDays)).ExecuteDeleteAsync());

            var otps = await TryDeleteAsync("SmsOtpCodes", () =>
                db.SmsOtpCodes.Where(CleanupRules.OldOtpCodes(now, otpDays)).ExecuteDeleteAsync());

            var reminderLogs = await TryDeleteAsync("SmsAutoReminderLogs", () =>
                db.SmsAutoReminderLogs.Where(CleanupRules.OldReminderLogs(now, reminderLogDays)).ExecuteDeleteAsync());

            var restockSubs = await TryDeleteAsync("RestockSubscriptions", () =>
                db.RestockSubscriptions.Where(CleanupRules.NotifiedRestockSubscriptions(now, restockDays)).ExecuteDeleteAsync());

            // ۷) ردیف‌های منقضی سشن (کش توزیع‌شده SQL Server خودش پاک‌سازی دوره‌ای ندارد)
            var sessions = await TryDeleteExpiredSessionsAsync(db, now);

            _logger.LogInformation(
                "Maintenance cleanup done: {Links} statement link(s), {SmsLogs} SMS log(s), {Outbox} outbox row(s), {Otps} OTP row(s), {Reminders} reminder log(s), {Restock} restock subscription(s), {Sessions} session row(s). Retention → SMS {SmsLogDays}d, outbox {OutboxDays}d, OTP {OtpDays}d, reminders {ReminderDays}d, restock {RestockDays}d.",
                links, smsLogs, outbox, otps, reminderLogs, restockSubs, sessions,
                smsLogDays, outboxDays, otpDays, reminderLogDays, restockDays);
        }

        /// <summary>
        /// ردیف‌های منقضی جدول سشن. این جدول موجودیت EF نیست (کش توزیع‌شده)، پس با SQL خام پاک می‌شود؛
        /// اگر نام/اسکیمای جدول تغییر کند، خطا فقط لاگ می‌شود.
        /// </summary>
        private async Task<int> TryDeleteExpiredSessionsAsync(SugarShopSalesDbContext db, DateTime now)
        {
            try
            {
                return await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM [dbo].[AppSessions] WHERE [ExpiresAtTime] < {0}", now);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "پاک‌سازی ردیف‌های منقضی سشن ناموفق بود (جدول AppSessions).");
                return 0;
            }
        }

        /// <summary>هر بخش پاک‌سازی مستقل است: خطای یکی، بقیه را متوقف نمی‌کند (کار شبانه نباید نیمه‌کاره بماند).</summary>
        private async Task<int> TryDeleteAsync(string table, Func<Task<int>> delete)
        {
            try
            {
                return await delete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "پاک‌سازی {Table} در کار شبانه نگهداری ناموفق بود", table);
                return 0;
            }
        }

        /// <summary>بازه نگهداری از appsettings؛ مقدار نامعتبر یا صفر و منفی نادیده گرفته می‌شود.</summary>
        private int RetentionDays(string key, int fallbackDays)
        {
            var configured = _configuration.GetValue<int?>(key);
            return configured is > 0 ? configured.Value : fallbackDays;
        }
    }
}
