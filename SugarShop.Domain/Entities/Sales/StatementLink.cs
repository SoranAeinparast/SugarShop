namespace SugarShop.Domain.Entities.Sales
{
    /// <summary>
    /// توکن اختصاصی و موقت دیدن صورت‌حساب پرداخت یک سفارش (بدون نیاز به ورود).
    /// این توکن در پیامک «صورت‌حساب پرداخت» برای مشتری فرستاده می‌شود و چون در دیتابیس ذخیره
    /// می‌شود، هم کوتاه است (هزینه پیامک کمتر) و هم قابل باطل‌کردن و قابل حسابرسی.
    /// توکن فقط اجازه «خواندن سند همان سفارش» را می‌دهد؛ هیچ تغییری در سفارش ایجاد نمی‌کند.
    /// </summary>
    public class StatementLink
    {
        public int Id { get; set; }

        /// <summary>رشته تصادفی امن (Base64Url) که در آدرس /s/{token} می‌آید.</summary>
        public string Token { get; set; } = string.Empty;

        public int OrderId { get; set; }

        /// <summary>صاحب سفارش در لحظه ساخت لینک (لایه دفاعی: اگر سفارش به کاربر دیگری برود، لینک باطل می‌شود).</summary>
        public string? UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>پایان اعتبار لینک (UTC) — پیش‌فرض چند روز.</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>آخرین باری که سند با این لینک باز شده است.</summary>
        public DateTime? LastOpenedAt { get; set; }

        /// <summary>تعداد بازدیدها؛ برای سنجش اثر پیامک‌های لینک‌دار.</summary>
        public int OpenCount { get; set; }

        public bool IsUsable(DateTime utcNow) => ExpiresAt > utcNow;
    }
}
