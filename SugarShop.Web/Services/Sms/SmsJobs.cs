using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SugarShop.Web.Services.Sms
{
    /// <summary>وظایف زمان‌بندی‌شده Hangfire برای سامانه پیامکی.</summary>
    public class SmsJobs
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmsJobs> _logger;

        public SmsJobs(IServiceScopeFactory scopeFactory, ILogger<SmsJobs> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// هر ساعت: یادآوری کوتاه پرداخت برای سفارش‌هایی که پیامک «آماده پرداخت» گرفته‌اند ولی بعد از
        /// تأخیر تعیین‌شده (پیش‌فرض ۲۴ ساعت) هنوز پرداخت نشده‌اند. ساعتی اجرا می‌شود تا تأخیر دقیقاً
        /// همان مقداری باشد که مدیر تنظیم کرده، نه «حداکثر یک روز بعد».
        /// </summary>
        public async Task RunPaymentRemindersAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sms = scope.ServiceProvider.GetRequiredService<SmsService>();

                var sent = await sms.SendPaymentRemindersAsync();
                if (sent > 0)
                    _logger.LogInformation("SMS payment reminders sent: {Count}.", sent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS payment reminder job failed.");
                throw; // Hangfire retry policy
            }
        }

        /// <summary>هر روز صبح: یادآوری تولد + بازگشت مشتری + هشدار موجودی کم.</summary>
        public async Task RunDailyAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sms = scope.ServiceProvider.GetRequiredService<SmsService>();

                var birthdays = await sms.SendBirthdayRemindersAsync();
                var winbacks = await sms.SendWinBackDiscountsAsync();
                var lowstock = await sms.CheckLowStockAsync();

                _logger.LogInformation("SMS daily job: {Birthdays} birthday, {Winbacks} win-back, {LowStock} low-stock.",
                    birthdays, winbacks, lowstock);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS daily reminders job failed.");
                throw; // Hangfire retry policy
            }
        }
    }
}
