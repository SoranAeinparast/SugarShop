using System;

namespace SugarShop.Domain.Entities
{
    /// <summary>تنظیمات هدر (هیرو) صفحه آموزش‌های شیرینی‌پزی — مشابه تنظیمات هدر گالری.</summary>
    public class EducationalHeaderSetting
    {
        public int Id { get; set; }

        // عنوان اصلی هدر
        public string Title { get; set; } = "آموزش‌های شیرینی‌پزی";

        // زیرعنوان
        public string Subtitle { get; set; } = "آموزش‌های حرفه‌ای و کاربردی برای شیرینی‌پزی خانگی";

        // تصویر پس‌زمینه (اختیاری)
        public string? BackgroundImagePath { get; set; }

        // رنگ پس‌زمینه (رنگ ساده یا گرادیان CSS)
        public string BackgroundColor { get; set; } = "linear-gradient(135deg, #f5e6d3 0%, #d4a056 100%)";

        // رنگ متن
        public string TextColor { get; set; } = "#3e2723";

        // ارتفاع هدر (پیکسل)
        public int Height { get; set; } = 300;

        // نمایش/عدم نمایش هدر
        public bool IsEnabled { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
