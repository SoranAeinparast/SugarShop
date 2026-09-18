using System;

namespace SugarShop.Domain.Entities.Sms
{
    /// <summary>لاگ ارسال پیامک — نگاشت به جدول موجود SmsLogs (ستون‌های جدید اختیاری/nullable هستند).</summary>
    public class SmsLog
    {
        public long Id { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string MessageText { get; set; } = "";
        public SmsProviderType ProviderUsed { get; set; } = SmsProviderType.SmsIr;
        public SmsSendStatus Status { get; set; } = SmsSendStatus.Pending;
        public string? ErrorMessage { get; set; }
        public decimal Cost { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // ── ردیابی تحویل (ستون‌های جدید — nullable تا با جدول موجود سازگار بماند) ──
        /// <summary>شناسه پیامک در sms.ir (messageId) برای استعلام وضعیت تحویل.</summary>
        public long? ProviderMessageId { get; set; }
        /// <summary>کد وضعیت دلیوری sms.ir: 1=رسیده به گوشی، 2=پردازش مخابرات، 3=رسیده مخابرات، 4=نرسیده مخابرات، 6=خطا، 7=لیست سیاه.</summary>
        public byte? DeliveryState { get; set; }
        /// <summary>آخرین باری که وضعیت تحویل از sms.ir استعلام شد.</summary>
        public DateTime? DeliveryCheckedAt { get; set; }
    }

    /// <summary>قالب پیامک هر سناریو — نگاشت به جدول موجود SmsTemplates.</summary>
    public class SmsTemplate
    {
        public int Id { get; set; }
        public SmsScenario Scenario { get; set; }
        public string Title { get; set; } = "";
        public string BodyText { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
