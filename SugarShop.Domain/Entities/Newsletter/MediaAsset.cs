using System;

namespace SugarShop.Domain.Entities
{
    public class MediaAsset
    {
        public int Id { get; set; }

        // اطلاعات فایل
        public string FileName { get; set; } = "";          // نام یکتای ذخیره‌شده (GUID)
        public string OriginalName { get; set; } = "";      // نام اصلی آپلودشده توسط کاربر
        public string FilePath { get; set; } = "";          // مسیر نسبی مثل /media/2026/08/xxx.jpg
        public string ThumbnailPath { get; set; } = "";     // مسیر بندانگشتی (اختیاری)

        // نوع و فرمت
        public string FileType { get; set; } = "General";   // Image, Video, Document, Audio
        public string MimeType { get; set; } = "";          // image/jpeg, video/mp4, ...
        public string Extension { get; set; } = "";         // .jpg, .pdf, ...

        // حجم
        public long FileSize { get; set; }                  // به بایت

        // دسته‌بندی و برچسب
        public string Category { get; set; } = "General";   // Products, Banners, Sliders, AboutUs, General
        public string Tags { get; set; } = "";              // برچسب‌های جداشده با کاما
        public string AltText { get; set; } = "";           // متن جایگزین (SEO)
        public string Description { get; set; } = "";       // توضیحات

        // آمار استفاده
        public int UsageCount { get; set; }                 // تعداد دفعات استفاده

        // تاریخ
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}