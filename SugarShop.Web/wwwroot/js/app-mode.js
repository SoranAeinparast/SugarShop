/* ═══════════════════════════════════════════════════════════
   SugarShop — تشخیص «کجا باز شده‌ایم؟» (اپلیکیشن / مرورگر / PWA)
   ═══════════════════════════════════════════════════════════
   نتیجه روی <html> به‌صورت data-app-mode می‌نشیند تا CSS و بقیه اسکریپت‌ها
   از یک منبع واحد استفاده کنند:

     inapp            → فقط داخل پوسته‌ی اندرویدی سایت (User-Agent: SugarShopApp/)
     android-browser  → مرورگر عادی کروم روی اندروید (intent:// کار می‌کند)
     browser          → حالت عادی وب (شامل PWA نصب‌شده روی گوشی یا دسکتاپ)

   قاعده طلایی: «اپ» فقط یعنی پوسته‌ی اندرویدی سایت.
   همین تصمیم در سمت سرور هم گرفته می‌شود (AppClient.IsAppRequest) و نتیجه‌اش همان اول
   روی <html> نوشته می‌شود؛ این اسکریپت فقط آن را تأیید و دقیق‌تر می‌کند (کروم اندروید در مقابل
   وب معمولی). پس چیدمان وب هرگز به چیدمان اپ تبدیل نمی‌شود و برعکس — بدون کوکی، بدون حافظه
   و بدون کش HTML؛ هر درخواست، حالت خودش را دارد.

   ⚠️ این فایل باید در <head> و پیش از رندر بدنه اجرا شود تا پرش ظاهری (flash) نباشد.
   در صفحه‌های بدون چیدمان سایت (مثل صورت‌حساب پیامکی) هم باید صریحاً صدا زده شود.
*/
(function () {
    'use strict';

    var root = document.documentElement;
    var ua = navigator.userAgent || '';

    // پوسته اندرویدی سایت، خودش را به انتهای User-Agent اضافه می‌کند (MainActivity: SugarShopApp/1.4)
    var androidShell = /SugarShopApp\//.test(ua);

    // تصمیم سمت سرور (روی <html> نوشته شده)؛ در صفحه‌های مستقل ممکن است خالی باشد
    var serverMode = root.getAttribute('data-app-mode');
    var serverSaysApp = serverMode === 'inapp';

    // حالت PWA نصب‌شده (WebAPK/TWA) دیگر «داخل اپلیکیشن» حساب نمی‌شود: پنجره‌ی اپ نصب‌شده همان
    // وب‌سایت است و کاربر باید همان چیدمان مرورگر را ببیند (وگرنه با کلیک روی دکمه‌های چاپ/دانلود
    // یا بازگشت از درگاه پرداخت، صفحه ناگهان به چیدمان اپ تغییر می‌کرد).
    var standalone = (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches)
        || window.navigator.standalone === true;

    // هر صفحه‌ی برنامه حالت را سمت سرور تعیین می‌کند؛ UA فقط برای صفحه‌های مستقلی که
    // هنوز data-app-mode ندارند fallback است و نمی‌تواند حالت رندرشده را عوض کند.
    var hasServerMode = serverMode === 'inapp' || serverMode === 'browser';
    var inApp = hasServerMode ? serverSaysApp : androidShell;
    var isAndroid = /Android/i.test(ua);

    if (serverMode && serverSaysApp !== inApp && window.console) {
        // ناهم‌خوانی سرور و کلاینت فقط از دستکاری UA ممکن است؛ برای تشخیص سریع لاگ می‌شود.
        console.warn('[app-mode] server says inapp=' + serverSaysApp + ' but UA says inapp=' + inApp);
    }

    // دکمه «باز کردن در اپلیکیشن» فقط در مرورگر عادی کروم معنا دارد؛
    // WebView داخل تلگرام/اینستاگرام (با نشانه ; wv) و داخل خود اپ، intent:// را اجرا نمی‌کند.
    var chromeAndroid = !inApp && isAndroid && /Chrome\//.test(ua) && !/;\s*wv\)/.test(ua);

    // 🏳️ تصمیم نهایی:
    // حالت سرور مرجع کامل است: اختلاف UA سمت کلاینت نباید چیدمان صفحه را در هیچ جهتی
    // عوض کند. صفحات بدون حالت سروری از UA استفاده می‌کنند؛ کروم اندروید فقط یک نوع مرورگر است.
    var mode = inApp ? 'inapp' : (chromeAndroid ? 'android-browser' : 'browser');
    if (root.getAttribute('data-app-mode') !== mode) {
        root.setAttribute('data-app-mode', mode);
    }

    // 🧹 هیچ کوکی‌ای برای «حالت اپ» نوشته نمی‌شود.
    // کوکی appmode نسخه‌های قبلی منبع اصلی به‌هم‌ریختگی بود: چون سرور از روی کوکی (نه UA)
    // تصمیم می‌گرفت، یک مرورگر عادی هم می‌توانست صفحه اصلی اپ را بگیرد و بعد از چاپ/دانلود
    // یک سند، با برگشتن به سایت، کاربر انگار به «دنیایی دیگر» برگشته باشد.
    // اگر کوکی قدیمی روی مرورگر مانده باشد، اینجا بی‌اثر و پاک می‌شود.
    try {
        if (document.cookie.indexOf('appmode=') !== -1) {
            document.cookie = 'appmode=; path=/; max-age=0; samesite=lax';
        }
    } catch (e) { /* اگر کوکی خوانا نبود، مشکلی پیش نمی‌آید */ }

    // نقطه ورود مشترک برای اسکریپت‌های دیگر (مثل نوار «باز کردن در اپلیکیشن»)
    window.appMode = {
        mode: mode,
        inApp: inApp,
        androidShell: androidShell,
        standalone: standalone,
        chromeAndroid: chromeAndroid
    };
})();
