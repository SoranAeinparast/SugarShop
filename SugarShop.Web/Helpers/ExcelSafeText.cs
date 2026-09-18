using System.Text;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// آماده‌سازی متن برای سلول‌های اکسل (ClosedXML/OpenXML): کاراکترهای کنترلی که استاندارد
    /// OpenXML قبول نمی‌کند حذف می‌شوند تا ساخت فایل با خطا متوقف نشود.
    /// مقادیر به‌صورت «متن» نوشته می‌شوند، پس رشته‌های شروع‌شده با = + - @ هرگز به‌عنوان فرمول
    /// اجرا نمی‌شوند (تزریق فرمول در خروجی اکسل واقعی ذاتاً بی‌اثر است).
    /// </summary>
    public static class ExcelSafeText
    {
        public static string Clean(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r')
                    continue;
                builder.Append(ch);
            }

            return builder.ToString();
        }
    }
}
