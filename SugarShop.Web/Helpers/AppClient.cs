using System;
using Microsoft.AspNetCore.Http;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// تشخیص «این درخواست از داخل اپلیکیشن آمده؟» در سمت سرور.
    ///
    /// تنها نشانه: پوسته اندرویدی سایت خودش را در User-Agent معرفی می‌کند
    /// (MainActivity: SugarShopApp/1.4) — قطعی، مخصوص هر درخواست و بدون نیاز به هیچ
    /// تنظیمی در سمت کلاینت.
    ///
    /// ⚠️ هیچ کوکی‌ای در این تشخیص نقش ندارد. کوکی «appmode» نسخه‌های قبلی (که برای
    /// حالت PWA نوشته می‌شد) باعث می‌شد مرورگر عادی هم قربانی چیدمان اپ شود: کاربر
    /// بعد از چاپ/دانلود یک سند، با برگشتن به سایت، صفحه اصلی اپ را می‌دید. حالا
    /// «اپ» فقط یعنی اپ اندرویدی؛ PWA نصب‌شده و مرورگر همیشه چیدمان وب می‌گیرند.
    /// </summary>
    public static class AppClient
    {
        public const string UserAgentMarker = "SugarShopApp/";

        public static bool IsAppRequest(HttpContext? context)
        {
            if (context == null) return false;

            var ua = context.Request.Headers.UserAgent.ToString();
            return !string.IsNullOrEmpty(ua)
                && ua.Contains(UserAgentMarker, StringComparison.OrdinalIgnoreCase);
        }
    }
}
