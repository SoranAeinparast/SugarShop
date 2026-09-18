using System;
using System.Globalization;

namespace SugarShop.Web.Helpers
{
    public static class PersianDateHelper
    {
        // تبدیل ارقام فارسی/عربی به انگلیسی تا ورودی‌های پیکر تاریخ شمسی (مثلاً ۱۴۰۴/۰۳/۱۵) قابل parse باشند
        private static string NormalizeDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
                else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        public static DateTime? ConvertPersianToDateTime(string persianDate, string time)
        {
            try
            {
                var dateParts = NormalizeDigits(persianDate).Split('/');
                if (dateParts.Length != 3) return null;
                int year = int.Parse(dateParts[0]);
                int month = int.Parse(dateParts[1]);
                int day = int.Parse(dateParts[2]);
                var timeParts = NormalizeDigits(time).Split(':');
                int hour = timeParts.Length > 0 ? int.Parse(timeParts[0]) : 0;
                int minute = timeParts.Length > 1 ? int.Parse(timeParts[1]) : 0;
                var persianCalendar = new PersianCalendar();
                return persianCalendar.ToDateTime(year, month, day, hour, minute, 0, 0);
            }
            catch
            {
                return null;
            }
        }
    }
}