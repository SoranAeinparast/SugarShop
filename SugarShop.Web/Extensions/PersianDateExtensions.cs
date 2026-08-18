using System.Globalization;
using System.Text.RegularExpressions;


namespace SugarShop.Web.Extensions
{
    public static class PersianDateExtensions
    {
        private static readonly PersianCalendar PersianCalendar = new();

        public static string ToPersianDateString(this DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue) return string.Empty;
            int year = PersianCalendar.GetYear(dateTime);
            int month = PersianCalendar.GetMonth(dateTime);
            int day = PersianCalendar.GetDayOfMonth(dateTime);
            return $"{year}/{month:D2}/{day:D2}";
        }
        public static string StripHtml(this string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public static string TruncateText(this string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.Length <= maxLength) return input;
            return input.Substring(0, maxLength) + "...";
        }
    }
}