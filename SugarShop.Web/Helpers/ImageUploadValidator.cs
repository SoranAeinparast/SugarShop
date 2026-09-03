using System;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// اعتبارسنجی فایل‌های تصویری آپلودشده (پروفایل/کیک) برای جلوگیری از
    /// آپلود فایل‌های اجرایی یا اسکریپت‌پذیر (aspx/html و…) در پوشه wwwroot.
    /// </summary>
    public static class ImageUploadValidator
    {
        public static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

        /// <summary>حداکثر حجم تصویر آواتار (۲ مگابایت).</summary>
        public const long MaxAvatarBytes = 2 * 1024 * 1024;

        /// <summary>حداکثر حجم تصویر سفارش کیک (۱۰ مگابایت).</summary>
        public const long MaxCakeImageBytes = 10 * 1024 * 1024;

        public static bool IsValidImage(IFormFile? file, long maxBytes, out string? error)
        {
            error = null;

            if (file == null || file.Length == 0)
            {
                error = "فایلی انتخاب نشده است.";
                return false;
            }

            if (file.Length > maxBytes)
            {
                error = $"حجم فایل نباید بیشتر از {Math.Max(1, maxBytes / (1024 * 1024))} مگابایت باشد.";
                return false;
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(AllowedExtensions, ext.ToLowerInvariant()) < 0)
            {
                error = "فقط تصاویر با فرمت jpg، jpeg، png، gif، webp یا bmp مجاز هستند.";
                return false;
            }

            return true;
        }
    }
}
