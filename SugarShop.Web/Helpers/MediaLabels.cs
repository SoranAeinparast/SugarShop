using System;
using System.Collections.Generic;
using System.Linq;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// برچسب‌های فارسی کتابخانه رسانه.
    /// مقادیر ذخیره‌شده در دیتابیس (Category/FileType) همان مقادیر انگلیسی و پایدار قبلی می‌مانند
    /// تا داده‌های موجود و فیلترها نشکنند؛ فقط «نام نمایشی» فارسی می‌شود.
    /// </summary>
    public static class MediaLabels
    {
        /// <summary>دسته‌بندی‌های مجاز به‌همراه نام فارسی.</summary>
        public static readonly IReadOnlyList<(string Value, string Label)> Categories = new List<(string, string)>
        {
            ("General", "عمومی"),
            ("Products", "محصولات"),
            ("Banners", "بنرها"),
            ("Sliders", "اسلایدرها"),
            ("AboutUs", "درباره ما"),
            ("Blog", "وبلاگ"),
            ("Gallery", "گالری")
        };

        /// <summary>انواع فایل به‌همراه نام فارسی.</summary>
        public static readonly IReadOnlyList<(string Value, string Label)> FileTypes = new List<(string, string)>
        {
            ("Image", "تصویر"),
            ("Icon", "آیکون"),
            ("Video", "ویدئو"),
            ("Document", "سند"),
            ("Audio", "صوتی"),
            ("General", "سایر")
        };

        /// <summary>مقادیر دسته‌بندی برای ViewBag (شامل «All» در ابتدا).</summary>
        public static string[] CategoryValues(bool includeAll = false)
            => (includeAll ? new[] { "All" } : Array.Empty<string>())
                .Concat(Categories.Select(c => c.Value))
                .ToArray();

        /// <summary>مقادیر نوع فایل برای ViewBag (شامل «All» در ابتدا).</summary>
        public static string[] FileTypeValues(bool includeAll = false)
            => (includeAll ? new[] { "All" } : Array.Empty<string>())
                .Concat(FileTypes.Select(t => t.Value))
                .ToArray();

        /// <summary>نام فارسی دسته‌بندی؛ مقدار ناشناخته بدون تغییر برگردانده می‌شود.</summary>
        public static string CategoryLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "—";
            if (value == "All") return "همه دسته‌ها";
            var found = Categories.FirstOrDefault(c => string.Equals(c.Value, value, StringComparison.OrdinalIgnoreCase));
            return found.Value == null ? value : found.Label;
        }

        /// <summary>نام فارسی نوع فایل؛ مقدار ناشناخته بدون تغییر برگردانده می‌شود.</summary>
        public static string FileTypeLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "—";
            if (value == "All") return "همه انواع";
            var found = FileTypes.FirstOrDefault(t => string.Equals(t.Value, value, StringComparison.OrdinalIgnoreCase));
            return found.Value == null ? value : found.Label;
        }
    }
}
