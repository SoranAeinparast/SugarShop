using System;

namespace SugarShop.Domain.Entities
{
    public class GalleryHeaderSetting
    {
        public int Id { get; set; }

        // عنوان اصلی هدر
        public string Title { get; set; } = "گالری تصاویر و ویدئوها";

        // زیرعنوان
        public string Subtitle { get; set; } = "نمونه‌ای از آثار هنری و شیرینی‌های خاص ما";

        // تصویر پس‌زمینه (اختیاری)
        public string? BackgroundImagePath { get; set; }

        // رنگ پس‌زمینه (گرادیان یا رنگ ساده)
        public string BackgroundColor { get; set; } = "linear-gradient(135deg, #667eea 0%, #764ba2 100%)";

        // رنگ متن
        public string TextColor { get; set; } = "#ffffff";

        // ارتفاع هدر (پیکسل)
        public int Height { get; set; } = 300;

        // نمایش/عدم نمایش هدر
        public bool IsEnabled { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}