using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarShop.Domain.Entities
{
    public class CategoryHeaderSetting
    {
        public int Id { get; set; }

        // ارتباط با جدول دسته‌بندی‌ها
        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        // ✅ نام‌های یکسان با کوئری SQL
        public string? DefaultTitle { get; set; }
        public string? DefaultSubtitle { get; set; }
        public string? BoxSelectionSubtitle { get; set; }

        // تصویر پس‌زمینه
        public string? BackgroundImagePath { get; set; }

        // رنگ پس‌زمینه
        public string BackgroundColor { get; set; } = "#ffffff";

        // رنگ متن
        public string TextColor { get; set; } = "#2c3e50";

        // ارتفاع
        public int Height { get; set; } = 200;

        // فعال/غیرفعال
        public bool IsEnabled { get; set; } = true;

        // استفاده از تصویر
        public bool UseBackgroundImage { get; set; } = false;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}