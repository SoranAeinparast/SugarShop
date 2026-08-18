using System;
using System.Globalization;

namespace SugarShop.Web.Helpers
{
    public static class PersianDateHelper
    {
        public static DateTime? ConvertPersianToDateTime(string persianDate, string time)
        {
            try
            {
                var dateParts = persianDate.Split('/');
                if (dateParts.Length != 3) return null;
                int year = int.Parse(dateParts[0]);
                int month = int.Parse(dateParts[1]);
                int day = int.Parse(dateParts[2]);
                var timeParts = time.Split(':');
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