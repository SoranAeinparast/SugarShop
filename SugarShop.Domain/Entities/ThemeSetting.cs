using System;

namespace SugarShop.Domain.Entities
{
    public enum HeaderType
    {
        Normal = 0,
        Sticky = 1,
        Fixed = 2
    }

    public enum FooterType
    {
        Normal = 0,
        Sticky = 1,
        Fixed = 2 
    }
    public class ThemeSetting
    {
        public int Id { get; set; }
        public string PrimaryColor { get; set; } = "#d4a056";
        public string SecondaryColor { get; set; } = "#f8f9fa";
        public string HeaderBgColor { get; set; } = "#ffffff";
        public string HeaderTextColor { get; set; } = "#333333";
        public string FooterBgColor { get; set; } = "#1e2a3a";
        public string FooterTextColor { get; set; } = "#cccccc";
        public int HeaderTransparency { get; set; } = 100;
        public int FooterTransparency { get; set; } = 100;
        public string BodyBgColor { get; set; } = "#f4f6f9";
        public string BodyTextColor { get; set; } = "#212529";
        public HeaderType HeaderType { get; set; } = HeaderType.Sticky;
        public FooterType FooterType { get; set; } = FooterType.Normal;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool BodyDotPatternEnabled { get; set; } = false;

        // ── تنظیمات اپلیکیشن موبایل ──
        /// <summary>نمایش/عدم نمایش اپلیکیشن در سایت (بنر نصب، لینک‌ها، صفحه دانلود).</summary>
        public bool AppEnabled { get; set; } = true;
        /// <summary>نام نمایشی اپلیکیشن — خالی = نام سایت.</summary>
        public string? AppDisplayName { get; set; }
        /// <summary>URL پایه اپلیکیشن (مثلاً https://sweets.soransoftpro.ir) — خالی = دامنه فعلی سایت.</summary>
        public string? AppBaseUrl { get; set; }
        /// <summary>فعال بودن نسخه اندروید (APK).</summary>
        public bool AppAndroidEnabled { get; set; } = true;
        /// <summary>فعال بودن راهنمای نصب iOS / PWA.</summary>
        public bool AppIosEnabled { get; set; } = true;
    }
}