using System;
using System.Collections.Generic;

namespace SugarShop.Domain.Entities.Sms
{
    /// <summary>
    /// تنظیمات متمرکز سامانه پیامکی (رکورد واحد Id=1). شامل اتصال، ساعات مجاز،
    /// محدودیت‌های ضد سوءاستفاده، خودکارِ تخفیف بازگشت و مسیریابی اطلاع‌رسانی کارکنان.
    /// </summary>
    public class SmsSystemSetting
    {
        public int Id { get; set; }

        // ── اتصال به درگاه ──
        public bool IsEnabled { get; set; } = true;
        public string ApiKey { get; set; } = "";
        public string SenderNumber { get; set; } = "";
        public bool SandboxMode { get; set; } = true; // تا زمانی که مدیر «تست اتصال» را نزده، واقعی ارسال نکن

        // ── ساعات مجاز ارسال (به وقت ایران) ──
        public bool RespectQuietHours { get; set; } = true;
        public int QuietStartHour { get; set; } = 22; // از ۲۲
        public int QuietEndHour { get; set; } = 8;    // تا ۸ صبح (تراکنشی همیشه مجاز است)

        // ── حفاظت از هزینه (ضد سوءاستفاده) ──
        public int OtpPerPhonePerHour { get; set; } = 3;      // حداکثر کد OTP برای هر شماره در ساعت
        public int OtpPerIpPerHour { get; set; } = 10;        // حداکثر کد OTP برای هر IP در ساعت
        public int MaxSmsPerPhonePerDay { get; set; } = 6;    // سقف کل پیامک‌های تبلیغاتی روزانه هر شماره
        public int MonthlyBudgetToman { get; set; } = 0;      // 0 = بدون سقف

        // ── صف ارسال ──
        public int QueueBatchSize { get; set; } = 20;         // تعداد پیامک در هر تیک صف پس‌زمینه
        public int QueueDelaySeconds { get; set; } = 2;       // فاصله ارسال بین پیامک‌های انبوه (ضد اسپم‌بلاک)

        // ── پیامک ورود/ثبت‌نام ──
        public bool OtpLoginEnabled { get; set; } = true;     // ورود/ثبت‌نام با شماره موبایل + کد
        public bool RegisterWelcomeEnabled { get; set; } = true;
        public bool PasswordResetSmsEnabled { get; set; } = true;

        // ── اطلاع‌رسانی سفارش‌ها به کارکنان ──
        public bool StaffAlertsEnabled { get; set; } = true;
        public string NewOrderAlertRoles { get; set; } = "Admin,OrderManager,Chef"; // نقش‌هایی که هنگام ثبت سفارش پیامک بگیرند (Owner خارج از گزینه‌هاست)
        public bool NewOrderAlertOnlyWeighing { get; set; } = true; // فقط درخواست‌های کیک سفارشی برای سرآشپز مهم است
        public string NewOrderAlertPhones { get; set; } = "";  // شماره‌های دستی اضافه (با کاما)

        // ── کیک سفارشی ──
        public bool CustomCakeAlertsEnabled { get; set; } = true;
        public string CustomCakeAlertPhones { get; set; } = "";

        // ── یادآوری پرداخت مرحله دوم (پس از وزن‌کشی) ──
        public bool PaymentReminderEnabled { get; set; } = true;
        public int PaymentReminderDelayHours { get; set; } = 24; // چند ساعت بعد از پیامک «آماده پرداخت» یادآوری برود

        // ── موجودی ──
        public bool RestockAlertsEnabled { get; set; } = true;
        public bool LowStockAlertsEnabled { get; set; } = true;
        public int LowStockThreshold { get; set; } = 5;
        public string LowStockAlertPhones { get; set; } = "";

        // ── تیکت‌ها ──
        public bool TicketNotificationsEnabled { get; set; } = true;

        // ── یادآوری تولد ──
        public bool BirthdaySmsEnabled { get; set; } = true;
        public int BirthdayDaysBefore { get; set; } = 3; // چند روز مانده به تولد پیامک دعوت برود

        // ── بازگشت مشتری + کد تخفیف خودکار ──
        public bool WinBackEnabled { get; set; } = true;
        public int WinBackDays { get; set; } = 30;             // چند روز از آخرین خرید گذشته باشد
        public int WinBackDiscountPercent { get; set; } = 10;  // درصد کد تخفیف خودکار
        public int WinBackDiscountValidityDays { get; set; } = 7;
        public int WinBackMinOrderAmount { get; set; } = 0;    // تومان؛ 0 = بدون حداقل
        public string WinBackCodeTitle { get; set; } = "کد بازگشت مشتری (خودکار)"; // عنوان کد در گزارش کدهای تخفیف
        public string WinBackCodePrefix { get; set; } = "BACK"; // پیشوند/عبارت کد تخفیف خودکار

        // ── مشتریان VIP ──
        public int VipPurchaseThresholdToman { get; set; } = 5000000; // مجموع خرید برای لقب VIP
        public bool VipAutoSpecialOffers { get; set; } = true;        // دریافت خودکار پیشنهادهای ویژه

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// صندوق خروجی پیامک (Outbox ماندگار در دیتابیس).
    /// هر پیامک قبل از ارسال در دیتابیس ثبت می‌شود؛ بنابراین با ری‌استارت شدن سایت،
    /// هیچ پیامکی گم نمی‌شود و صف از همان‌جا ادامه پیدا می‌کند.
    /// </summary>
    public class SmsOutboxItem
    {
        public long Id { get; set; }
        public string Phone { get; set; } = "";
        public string Message { get; set; } = "";
        public SmsScenario Scenario { get; set; }
        public SmsRecipientType RecipientType { get; set; } = SmsRecipientType.Customer;
        public SmsSendStatus Status { get; set; } = SmsSendStatus.Pending; // Pending | Processing | Sent | Failed
        public int Attempts { get; set; } = 0;
        public string? ErrorMessage { get; set; }
        public string? RefKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
    }

    /// <summary>دفترچه کدهای OTP (ورود و بازیابی رمز).</summary>
    public class SmsOtpCode
    {
        public long Id { get; set; }
        public string Phone { get; set; } = "";
        public string Code { get; set; } = "";
        public string Purpose { get; set; } = "Login";     // Login | PasswordReset
        public string? IpAddress { get; set; }
        public int AttemptCount { get; set; } = 0;         // تعداد تلاش اشتباه برای این کد
        public bool IsUsed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(2);
    }

    /// <summary>قابلیت «خبرم کن وقتی موجود شد» برای هر محصول.</summary>
    public class RestockSubscription
    {
        public int Id { get; set; }
        public int SweetItemId { get; set; }
        public string UserId { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool Notified { get; set; } = false;
        public DateTime? NotifiedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>لاگ ارسال خودکار یادآوری‌ها (تولد / بازگشت / موجودی) برای جلوگیری از ارسال تکراری.</summary>
    public class SmsAutoReminderLog
    {
        public long Id { get; set; }
        public SmsReminderType ReminderType { get; set; }
        public string RefKey { get; set; } = "";   // Birthday:{id}:{yyyy-MM} یا WinBack:{userId}:{yyyy-MM} یا Restock:{itemId}
        public string Phone { get; set; } = "";
        public string Message { get; set; } = "";
        public bool Success { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>کمپین ارسال انبوه (تبلیغاتی / مناسبتی).</summary>
    public class SmsCampaign
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public SmsAudienceType Audience { get; set; }
        public string Message { get; set; } = "";
        public SmsCampaignStatus Status { get; set; } = SmsCampaignStatus.Draft;
        public int TotalRecipients { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public decimal TotalCost { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public List<SmsCampaignRecipient> Recipients { get; set; } = new();
    }

    /// <summary>گیرنده‌های هر کمپین + وضعیت ارسال به هرکدام.</summary>
    public class SmsCampaignRecipient
    {
        public long Id { get; set; }
        public int CampaignId { get; set; }
        public string Phone { get; set; } = "";
        public string? RecipientName { get; set; }
        public SmsSendStatus Status { get; set; } = SmsSendStatus.Pending;
        public string? ErrorMessage { get; set; }
        public decimal Cost { get; set; }
        public long? SmsLogId { get; set; }
        public DateTime? SentAt { get; set; }

        public SmsCampaign? Campaign { get; set; }
    }
}
