namespace SugarShop.Domain.Entities.Sales
{
    /// <summary>
    /// نشانه‌های داخلی که در ستون <see cref="Order.Notes"/> ذخیره می‌شوند و مسیر پردازش پرداخت
    /// را تعیین می‌کنند (شارژ کیف پول / پرداخت سفارش کیک سفارشی).
    /// این مقادیر هرگز نباید از ورودی آزاد مشتری پر شوند؛ در CheckoutController پاک‌سازی می‌شوند.
    /// </summary>
    public static class OrderNotes
    {
        /// <summary>سفارش موقتِ شارژ مستقیم کیف پول (بدون ردیف کالا).</summary>
        public const string WalletRecharge = "WalletRecharge";

        /// <summary>پیشوند سفارش موقتِ پرداخت کیک سفارشی؛ شناسه کیک پس از آن می‌آید.</summary>
        public const string CustomCakeOrderPrefix = "CustomCakeOrder_";

        public static string ForCustomCakeOrder(int cakeOrderId) => $"{CustomCakeOrderPrefix}{cakeOrderId}";

        /// <summary>شناسه کیک سفارشی را از مقدار Notes استخراج می‌کند (در صورت نامعتبر بودن، null).</summary>
        public static int? TryParseCustomCakeOrderId(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes) || !notes.StartsWith(CustomCakeOrderPrefix, System.StringComparison.Ordinal))
                return null;

            var raw = notes.Substring(CustomCakeOrderPrefix.Length);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
