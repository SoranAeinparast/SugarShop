using System;

namespace SugarShop.Domain.Entities
{
    /// <summary>
    /// لاگ دانلود اپلیکیشن اندروید — هر درخواست دانلود با متادیتا ثبت می‌شود
    /// تا داشبورد مدیریت بتواند آمار دقیق ارائه دهد.
    /// </summary>
    public class AppDownloadLog
    {
        public long Id { get; set; }

        /// <summary>مسیر فایل دانلودشده (مثلاً /app/pastry-app.apk) — برای آینده</summary>
        public string FilePath { get; set; } = "/app/pastry-app.apk";

        /// <summary>نسخه‌ی APK دانلودشده</summary>
        public string? AppVersion { get; set; }

        /// <summary>IP کاربر (برای شمارش دانلودهای یکتا)</summary>
        public string? IpAddress { get; set; }

        /// <summary>دستگاه/مرورگر</summary>
        public string? UserAgent { get; set; }

        /// <summary>آیا دانلود از طریق صفحه‌ی دانلود با QR انجام شد؟</summary>
        public string? Referrer { get; set; }

        /// <summary>زمان دانلود (UTC)</summary>
        public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    }
}
