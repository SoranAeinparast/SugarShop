using System.Linq.Expressions;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Domain.Entities.Sms;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// قواعد «چه ردیفی در کار شبانه پاک شود» — جدا از خود کار، تا هم یک‌جا خوانده شوند
    /// و هم بشود بدون دیتابیس و با لیست ساده آزمود.
    /// قاعده‌ها عمداً سخت‌گیرانه‌اند: هیچ ردیف تازه یا در جریانی حذف نمی‌شود.
    /// </summary>
    public static class CleanupRules
    {
        /// <summary>توکن صورت‌حساب: فقط بعد از گذشتن تاریخ اعتبار.</summary>
        public static Expression<Func<StatementLink, bool>> ExpiredStatementLinks(DateTime now)
            => link => link.ExpiresAt <= now;

        /// <summary>لاگ پیامک: قدیمی‌تر از بازه نگهداری (روز).</summary>
        public static Expression<Func<SmsLog, bool>> OldSmsLogs(DateTime now, int retentionDays)
            => log => log.SentAt < now.AddDays(-retentionDays);

        /// <summary>
        /// صف پیامک: فقط ردیف‌های تمام‌شده (ارسال‌شده یا ناموفق) و قدیمی.
        /// ردیف «در انتظار ارسال» یا «در حال ارسال» هرگز حذف نمی‌شود.
        /// </summary>
        public static Expression<Func<SmsOutboxItem, bool>> FinishedOutboxItems(DateTime now, int retentionDays)
            => item => item.CreatedAt < now.AddDays(-retentionDays)
                       && item.Status != SmsSendStatus.Pending
                       && item.Status != SmsSendStatus.Processing;

        /// <summary>کد یکبارمصرف کهنه (عمر خود کد دو دقیقه است).</summary>
        public static Expression<Func<SmsOtpCode, bool>> OldOtpCodes(DateTime now, int retentionDays)
            => code => code.CreatedAt < now.AddDays(-retentionDays);

        /// <summary>
        /// لاگ یادآوری خودکار. بازه باید دست‌کم چند ماه باشد، وگرنه کلید همین ماه پاک می‌شود و
        /// همان یادآوری در همان ماه دوباره ارسال می‌شود (تولد/بازگشت مشتری کلید ماهانه دارند).
        /// </summary>
        public static Expression<Func<SmsAutoReminderLog, bool>> OldReminderLogs(DateTime now, int retentionDays)
            => log => log.SentAt < now.AddDays(-retentionDays);

        /// <summary>اشتراک «موجود شد» که پیامکش رفته است (فقط سابقه؛ منطق ارسال به Notified نگاه می‌کند).</summary>
        public static Expression<Func<RestockSubscription, bool>> NotifiedRestockSubscriptions(DateTime now, int retentionDays)
            => sub => sub.Notified && sub.NotifiedAt != null && sub.NotifiedAt < now.AddDays(-retentionDays);
    }
}
