namespace SugarShop.Domain.Entities.Sms
{
    /// <summary>سناریوهای ارسال پیامک — برای هر سناریو یک قالب پیش‌فرض وجود دارد.</summary>
    public enum SmsScenario
    {
        Otp = 1,                       // کد یکبار مصرف ورود / ثبت‌نام
        PasswordResetOtp = 14,         // کد بازیابی رمز عبور
        RegisterWelcome = 2,           // خوش‌آمد پس از ثبت‌نام
        OrderPlacedCustomer = 3,       // ثبت سفارش جدید (مشتری)
        OrderPlacedStaff = 4,          // ثبت سفارش جدید (مدیر / مدیر فروش / سرآشپز)
        WeighingReadyCustomer = 5,     // وزن‌کشی انجام شد، سفارش آماده پرداخت است
        PaymentConfirmedCustomer = 6,  // پرداخت با موفقیت تأیید شد
        PaymentStatementCustomer = 18, // لینک صورت‌حساب پرداخت (پس از پرداخت موفق)
        PaymentReminderCustomer = 19,  // یادآوری پرداخت مرحله دوم (چند ساعت بعد از پیامک «آماده پرداخت»)
        OrderStatusChangedCustomer = 7,// تغییر وضعیت سفارش
        CustomCakeStatusCustomer = 15, // تغییر وضعیت کیک سفارشی
        TicketReceivedCustomer = 8,    // تیکت شما دریافت شد
        TicketAnsweredCustomer = 9,    // تیکت شما پاسخ داده شد
        BirthdayReminder = 10,         // یادآوری تولد (دعوت به سفارش کیک)
        WinBackDiscount = 11,          // «دلمان برایت تنگ شده...» + کد تخفیف
        RestockAvailable = 12,         // محصول منتظر‌شده موجود شد
        LowStockAlert = 16,            // هشدار موجودی کم (به مدیر)
        SpecialOffer = 13,             // اطلاع‌رسانی تخفیف ویژه / مراسمات (ارسال انبوه)
        TestMessage = 17               // پیامک تست از پنل مدیریت
    }

    /// <summary>وضعیت ارسال هر پیامک در لاگ.</summary>
    public enum SmsSendStatus
    {
        Pending = 0,
        Sent = 1,
        Failed = 2,
        BlockedQuietHours = 3,
        BlockedRateLimit = 4,
        BlockedDisabled = 5,
        Skipped = 6,
        Processing = 7  // فقط در صف خروجی (Outbox): در حال ارسال
    }

    /// <summary>نوع ارائه‌دهنده پیامک (مقدار int همان ستون ProviderUsed جدول SmsLogs).</summary>
    public enum SmsProviderType
    {
        SmsIr = 1,
        Kavenegar = 2,
        Ghasedak = 3
    }

    /// <summary>گیرنده قوانین اطلاع‌رسانی کارکنان.</summary>
    public enum SmsRecipientType
    {
        Customer = 1,
        Owner = 2,
        Admin = 3,
        OrderManager = 4,
        Chef = 5,
        CustomPhones = 6
    }

    /// <summary>مخاطبان کمپین پیام انبوه.</summary>
    public enum SmsAudienceType
    {
        AllCustomers = 1,            // همه کاربران دارای شماره موبایل
        PurchasedLast90Days = 2,     // خرید کرده در ۹۰ روز گذشته
        PurchasedLast180Days = 3,    // خرید کرده در ۶ ماه گذشته
        VipCustomers = 4,            // مشتریان VIP
        NoPurchase180Days = 5,       // بیش از ۶ ماه خرید نکرده‌اند
        SmsSubscribers = 6,          // مشترکین پیامکی (خبرنامه)
        BirthdaysThisMonth = 7       // تولدهای این ماه
    }

    public enum SmsCampaignStatus
    {
        Draft = 0,
        Sending = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }

    public enum SmsReminderType
    {
        Birthday = 1,
        WinBack = 2,
        RestockAlert = 3,
        LowStockAlert = 4,

        /// <summary>یادآوری پرداخت مرحله دوم برای سفارش وزن‌کشی‌شده‌ای که پرداخت نشده مانده.</summary>
        PaymentReminder = 5
    }
}
