using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SugarShop.Web.Helpers;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// ذخیره‌سازی تصاویر آپلودشده کاربران.
    /// این سرویس جای متد تکراری <c>SaveFile</c> در کنترلرها را گرفته و
    /// اعتبارسنجی نوع/حجم فایل هم داخل خودش انجام می‌شود تا هیچ مسیری
    /// نتواند بدون بررسی، فایل دلخواه (مثلاً .aspx/.html/.exe) را در wwwroot بنویسد.
    /// </summary>
    public class ImageStorageService
    {
        /// <summary>پوشه‌ای که تصاویر سفارش کیک در آن ذخیره می‌شود (نسبت به ریشه wwwroot).</summary>
        public const string CakeOrderFolder = "images/cake_orders";

        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<ImageStorageService> _logger;

        public ImageStorageService(IWebHostEnvironment webHostEnvironment, ILogger<ImageStorageService> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        /// <summary>
        /// اعتبارسنجی و ذخیره یک تصویر.
        /// در صورت نامعتبر بودن فایل، مسیر ذخیره‌شده <c>null</c> و متن خطا برگردانده می‌شود
        /// تا کنترلر بتواند آن را به ModelState اضافه کند؛ در این حالت هیچ فایلی نوشته نمی‌شود.
        /// </summary>
        /// <param name="file">فایل آپلودشده.</param>
        /// <param name="prefix">پیشوند نام فایل برای دسته‌بندی (مثلاً cake_sample).</param>
        /// <param name="maxBytes">حداکثر حجم مجاز.</param>
        /// <param name="folder">پوشه ذخیره‌سازی؛ پیش‌فرض پوشه تصاویر سفارش کیک است.</param>
        public async Task<(string? Path, string? Error)> SaveImageAsync(
            IFormFile? file,
            string prefix,
            long maxBytes,
            string folder = CakeOrderFolder)
        {
            if (file == null || file.Length == 0)
                return (null, null);

            if (!ImageUploadValidator.IsValidImage(file, maxBytes, out var error))
                return (null, error);

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // نام فایل کاملاً سمت سرور ساخته می‌شود تا نام ارسالی کاربر (مسیر/کاراکتر خطرناک) بی‌اثر باشد
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{prefix}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ذخیره تصویر آپلودشده در {Path} ناموفق بود", filePath);
                return (null, "ذخیره تصویر ناموفق بود. لطفاً دوباره تلاش کنید.");
            }

            return ($"/{folder.TrimEnd('/')}/{uniqueFileName}", null);
        }

        /// <summary>حذف فایل ذخیره‌شده قبلی (مثلاً هنگام جایگزینی تصویر)؛ نبود فایل خطا محسوب نمی‌شود.</summary>
        public void DeleteIfExists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            try
            {
                var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(physicalPath))
                    File.Delete(physicalPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "حذف فایل {Path} ناموفق بود", relativePath);
            }
        }
    }
}
