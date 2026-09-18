using System.Text;

namespace SugarShop.Web.Extensions
{
    public static class PersianNumberExtensions
    {
        private static readonly char[] EnglishDigits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        private static readonly char[] PersianDigits = { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };

        public static string ToPersianNumber(this int number)
        {
            return number.ToString().ToPersianNumber();
        }

        public static string ToPersianNumber(this long number)
        {
            return number.ToString().ToPersianNumber();
        }

        /// <summary>
        /// تبدیل ارقام فارسی/عربی به ارقام ASCII؛ برای جست‌وجوی سمت سرور روی کد سفارش و شماره تماس،
        /// چون کاربر ممکن است عبارت را با صفحه‌کلید فارسی تایپ کند («۱۲۳۴» در برابر «1234»).
        /// </summary>
        public static string ToEnglishNumber(this string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty;

            var result = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                // ارقام فارسی (U+06F0..U+06F9) و ارقام عربی (U+0660..U+0669)
                if (c >= '\u06F0' && c <= '\u06F9')
                    result.Append(EnglishDigits[c - '\u06F0']);
                else if (c >= '\u0660' && c <= '\u0669')
                    result.Append(EnglishDigits[c - '\u0660']);
                else
                    result.Append(c);
            }
            return result.ToString();
        }

        public static string ToPersianNumber(this string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty;

            var result = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= '0' && c <= '9')
                {
                    // فقط ارقام ASCII انگلیسی به فارسی تبدیل می‌شوند
                    result.Append(PersianDigits[c - '0']);
                }
                else
                {
                    // ارقام فارسی/عربی و سایر کاراکترها بدون تغییر حفظ می‌شوند
                    // (char.IsDigit برای ارقام یونیکد مثل ۰۹۱۲ هم true برمی‌گرداند
                    //  و محاسبه c - '0' ایندکس خارج از محدوده می‌ساخت)
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }
}