using System;

namespace SugarShop.Domain.Entities.Sales
{
    /// <summary>نوع لینکی که در پیامک برای مشتری رفته است.</summary>
    public enum SmsLinkKind
    {
        /// <summary>لینک پرداخت/سفارش در پیامک «سفارش شما آماده پرداخت است» (آدرس کوتاه /p/{id} و /o/{id}).</summary>
        WaitingPayment = 1,

        /// <summary>لینک اختصاصی صورت‌حساب در پیامک «صورت‌حساب پرداخت آماده است» (آدرس کوتاه /s/{token}).</summary>
        Statement = 2
    }

    /// <summary>
    /// اثرسنجی پیامک‌های لینک‌دار: برای هر سفارش و هر نوع لینک، «چه زمانی پیامک رفت»
    /// و «چند بار لینک باز شد» ثبت می‌شود.
    ///
    /// چرا جدول جدا و نه شمارش از لاگ پیامک؟ چون لاگ پیامک فقط شماره و متن را دارد و
    /// به سفارش گره نخورده است؛ بدون این جدول نمی‌شد گفت «از میان سفارش‌هایی که پیامک
    /// لینک‌دار گرفتند، چند درصد لینک را باز کردند و چند نفر پرداخت کردند».
    ///
    /// این داده فقط برای گزارش است و هیچ اثری بر پرداخت یا وضعیت سفارش ندارد؛
    /// به همین دلیل اگر یک بازدید در شرایط هم‌زمانی نادر شمرده نشود، مشکلی پیش نمی‌آید.
    /// </summary>
    public class SmsLinkTracking
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public SmsLinkKind Kind { get; set; }

        /// <summary>زمانی که پیامک حاوی این لینک به صف ارسال سپرده شد (UTC).</summary>
        public DateTime? SentAt { get; set; }

        /// <summary>اولین باری که لینک باز شد (UTC) — مبنای محاسبه «نرخ باز شدن».</summary>
        public DateTime? FirstOpenedAt { get; set; }

        /// <summary>آخرین بازدید از لینک (UTC).</summary>
        public DateTime? LastOpenedAt { get; set; }

        /// <summary>تعداد بازدیدهای لینک (تقریبی؛ برای مقایسه نسبی پیامک‌ها).</summary>
        public int OpenCount { get; set; }

        public bool WasOpened => FirstOpenedAt.HasValue;
    }
}
