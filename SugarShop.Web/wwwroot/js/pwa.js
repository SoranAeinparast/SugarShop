/* ═══════════════════════════════════════════════════════════
   SugarShop PWA — منطق سمت کلاینت
   ثبت Service Worker، بنر نصب (همیشه‌قابل‌دیدن)، راهنمای نصب، نشان سبد، نوار آفلاین
   ═══════════════════════════════════════════════════════════ */
(function () {
    'use strict';

    // ── ۱) ثبت Service Worker ──
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js', { scope: '/' })
                .then(function (reg) {
                    reg.addEventListener('updatefound', function () {
                        var nw = reg.installing;
                        if (!nw) return;
                        nw.addEventListener('statechange', function () {
                            if (nw.state === 'installed' && navigator.serviceWorker.controller) {
                                nw.postMessage({ type: 'SKIP_WAITING' });
                            }
                        });
                    });
                })
                .catch(function () { /* بی‌صدا: سایت بدون SW هم کار می‌کند */ });
        });

        var refreshed = false;
        navigator.serviceWorker.addEventListener('controllerchange', function () {
            if (!refreshed) {
                refreshed = true;
                location.reload();
            }
        });
    }

    // ── ۲) بنر نصب اپ ──
    // قانون نمایش: فقط وقتی «همین حالا اپ هستیم» یا «اخیراً بسته شده» مخفی می‌ماند.
    // به رویداد beforeinstallprompt وابسته نیستیم — آن رویداد وقتی اپ قبلاً نصب شده
    // یا مرورگر هنوز آماده نیست هرگز اجرا نمی‌شود و بنر بی‌سبب ناپدید می‌شد.
    var deferredPrompt = null;
    var banner = document.getElementById('pwaInstallBanner');
    var DISMISS_KEY = 'pwaInstallDismissedAt_v2'; // کلید جدید: پرچم‌های بسته‌شده قبلی بی‌اثر می‌شوند
    var DISMISS_DAYS = 3;

    function isStandalone() {
        return window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
    }

    function recentlyDismissed() {
        var dismissed = parseInt(localStorage.getItem(DISMISS_KEY) || '0', 10);
        return dismissed && ((Date.now() - dismissed) / 86400000) < DISMISS_DAYS;
    }

    function shouldShowBanner() {
        if (!banner) return false;
        if (isStandalone()) return false;          // همین حالا داخل اپ هستیم
        if (recentlyDismissed()) return false;      // اخیراً بسته شده
        if (/SugarShopApp\//.test(navigator.userAgent)) return false; // داخل اپ اندرویدی
        return true;
    }

    function maybeShowBanner() {
        if (banner && shouldShowBanner()) banner.classList.remove('pwa-hidden');
    }

    // نمایش بلافاصله پس از لود — بدون انتظار برای هیچ رویدادی
    maybeShowBanner();

    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredPrompt = e;   // فقط برای دکمه نصب لازم است؛ نمایش بنر به آن وابسته نیست
        maybeShowBanner();
    });

    var installBtn = document.getElementById('pwaInstallBtn');
    if (installBtn) {
        installBtn.addEventListener('click', function () {
            // دکمه یک لینک به صفحه‌ی دانلود است — مسیر بومی مرورگر دست نمی‌زنیم؛
            // صفحه‌ی دانلود خودش مسیر درست هر پلتفرم را نشان می‌دهد.
        });
    }

    var dismissBtn = document.getElementById('pwaDismissBtn');
    if (dismissBtn) {
        dismissBtn.addEventListener('click', function () {
            if (banner) banner.classList.add('pwa-hidden');
            localStorage.setItem(DISMISS_KEY, String(Date.now()));
        });
    }

    window.addEventListener('appinstalled', function () {
        if (banner) banner.classList.add('pwa-hidden');
    });

    /// (راهنمای نصب حذف شد — دکمه نصب مستقیماً به صفحه‌ی دانلود /App/Download هدایت می‌کند)

    // ── ۳) نوار وضعیت آفلاین ──
    function updateOnlineBar() {
        var bar = document.getElementById('pwaOfflineBar');
        if (bar) bar.style.display = navigator.onLine ? 'none' : 'block';
    }
    window.addEventListener('online', updateOnlineBar);
    window.addEventListener('offline', updateOnlineBar);
    updateOnlineBar();

    // ── ۴) نشان تعداد سبد روی ناوبری پایین ──
    function refreshCartBadge() {
        var badge = document.getElementById('pwaCartBadge');
        if (!badge) return;
        fetch('/Cart/GetCartState', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (d) {
                if (!d) return;
                var n = (d.totalItems || 0);
                badge.textContent = n > 99 ? '99+' : n;
                badge.style.display = n > 0 ? 'flex' : 'none';
            })
            .catch(function () { });
    }
    if (document.getElementById('pwaCartBadge')) {
        refreshCartBadge();
        setInterval(refreshCartBadge, 8000);
        window.addEventListener('focus', refreshCartBadge);
    }
})();
