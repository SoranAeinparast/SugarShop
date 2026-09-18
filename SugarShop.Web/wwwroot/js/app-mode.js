/* ═══════════════════════════════════════════════════════════
   SugarShop — تشخیص «کجا باز شده‌ایم؟» (اپلیکیشن / مرورگر / PWA)
   ═══════════════════════════════════════════════════════════
   نتیجه روی <html> به‌صورت data-app-mode می‌نشیند تا CSS و بقیه اسکریپت‌ها
   از یک منبع واحد استفاده کنند:

     inapp            → داخل اپلیکیشن (پوسته اندرویدی سایت یا PWA نصب‌شده)
     android-browser  → مرورگر عادی کروم روی اندروید (intent:// کار می‌کند)
     browser          → حالت عادی وب

   این فایل باید در <head> و پیش از رندر بدنه اجرا شود تا پرش ظاهری (flash) نباشد.
   در صفحه‌های بدون چیدمان سایت (مثل صورتحساب پیامکی) هم باید صریحاً صدا زده شود.
*/
(function () {
    'use strict';

    var ua = navigator.userAgent || '';

    // پوسته اندرویدی سایت، خودش را به انتهای User-Agent اضافه می‌کند (MainActivity: SugarShopApp/1.4)
    var androidShell = /SugarShopApp\//.test(ua);

    // PWA نصب‌شده روی گوشی (WebAPK/TWA) یا هر محیطی که حالت standalone را گزارش کند
    var standalone = (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches)
        || window.navigator.standalone === true;

    var inApp = androidShell || standalone;
    var isAndroid = /Android/i.test(ua);

    // دکمه «باز کردن در اپلیکیشن» فقط در مرورگر عادی کروم معنا دارد؛
    // WebView داخل تلگرام/اینستاگرام (با نشانه ; wv) و داخل خود اپ، intent:// را اجرا نمی‌کند.
    var chromeAndroid = !inApp && isAndroid && /Chrome\//.test(ua) && !/;\s*wv\)/.test(ua);

    var mode = inApp ? 'inapp' : (chromeAndroid ? 'android-browser' : 'browser');
    document.documentElement.setAttribute('data-app-mode', mode);

    // سرور نمی‌تواند حالت standalone را ببیند؛ این کوکی به او می‌گوید که این کاربر در اپ است
    // تا بلوک‌های اپ‌محور (مثل صفحه اصلی اپ) از همان درخواست بعدی سمت سرور رندر شوند.
    try {
        if (inApp) {
            document.cookie = 'appmode=1; path=/; max-age=2592000; samesite=lax';
        } else if (document.cookie.indexOf('appmode=') !== -1) {
            document.cookie = 'appmode=; path=/; max-age=0; samesite=lax';
        }
    } catch (e) { /* بدون کوکی هم چه‌چیز خراب نمی‌شود */ }

    // نقطه ورود مشترک برای اسکریپت‌های دیگر (مثل نوار «باز کردن در اپلیکیشن»)
    window.appMode = {
        mode: mode,
        inApp: inApp,
        androidShell: androidShell,
        standalone: standalone,
        chromeAndroid: chromeAndroid
    };
})();
