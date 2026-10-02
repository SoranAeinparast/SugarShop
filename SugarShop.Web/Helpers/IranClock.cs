using System;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// منبع واحد «زمان ایران» (UTC+3:30).
    /// همه زمان‌های ذخیره‌شده در دیتابیس UTC هستند؛ این کلاس فقط برای محاسبه «مرزهای روز/ماه»
    /// در گزارش‌ها، سقف‌های روزانه و ساعات سکوت پیامک استفاده می‌شود تا آمار یک روز شمسی/ایرانی
    /// با روز UTC جابه‌جا نشود (قبلاً فروش ۰۰:۰۰ تا ۰۳:۳۰ ایران در آمار «دیروز» می‌افتاد).
    /// </summary>
    public static class IranClock
    {
        /// <summary>اختلاف ساعت ایران با UTC (ایران ساعت تابستانی ندارد).</summary>
        public static readonly TimeSpan Offset = TimeSpan.FromHours(3.5);

        /// <summary>زمان کنونی به وقت ایران.</summary>
        public static DateTime Now => DateTime.UtcNow + Offset;

        /// <summary>تاریخ امروز ایران (ساعت ۰۰:۰۰).</summary>
        public static DateTime Today => Now.Date;

        /// <summary>شروع یک روز ایران، بر حسب UTC — برای فیلترکردن ستون‌های UTC در دیتابیس.</summary>
        public static DateTime DayStartUtcFor(DateTime iranDay)
            => DateTime.SpecifyKind(iranDay.Date - Offset, DateTimeKind.Utc);

        /// <summary>پایان یک روز ایران (شروع روز بعد)، بر حسب UTC.</summary>
        public static DateTime DayEndUtcFor(DateTime iranDay) => DayStartUtcFor(iranDay.Date.AddDays(1));

        /// <summary>شروع امروز ایران، بر حسب UTC.</summary>
        public static DateTime DayStartUtc => DayStartUtcFor(Today);

        /// <summary>پایان امروز ایران، بر حسب UTC.</summary>
        public static DateTime DayEndUtc => DayEndUtcFor(Today);

        /// <summary>شروع ماه جاری ایران، بر حسب UTC.</summary>
        public static DateTime MonthStartUtc => DayStartUtcFor(new DateTime(Today.Year, Today.Month, 1));

        /// <summary>تاریخ روز ایرانِ یک زمان UTC (برای گروه‌بندی گزارش‌ها).</summary>
        public static DateTime IranDayOf(DateTime utc) => (utc + Offset).Date;
    }
}
