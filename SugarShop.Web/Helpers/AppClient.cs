using System;
using Microsoft.AspNetCore.Http;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// تشخیص «این درخواست از داخل اپلیکیشن آمده؟» در سمت سرور.
    ///
    /// دو نشانه داریم:
    /// ۱) پوسته اندرویدی سایت خودش را در User-Agent معرفی می‌کند (SugarShopApp/) — قطعی و
    ///    بدون نیاز به هیچ تنظیمی.
    /// ۲) کوکی appmode که اسکریپت سمت کلاینت (wwwroot/js/app-mode.js) برای حالت‌های
    ///    «PWA نصب‌شده» می‌گذارد؛ یک درخواست بعد از نصب فعال می‌شود.
    ///
    /// استفاده: بلوک‌هایی که فقط برای کاربر اپ معنا دارند (مثل صفحه اصلی اپ‌گونه) قبل از
    /// اجرا این بررسی را انجام می‌دهند تا بازدیدکننده وب کوئری و بار اضافه ندهد.
    /// </summary>
    public static class AppClient
    {
        public const string CookieName = "appmode";
        public const string UserAgentMarker = "SugarShopApp/";

        public static bool IsAppRequest(HttpContext? context)
        {
            if (context == null) return false;

            var ua = context.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrEmpty(ua) && ua.Contains(UserAgentMarker, StringComparison.OrdinalIgnoreCase))
                return true;

            return context.Request.Cookies[CookieName] == "1";
        }
    }
}
