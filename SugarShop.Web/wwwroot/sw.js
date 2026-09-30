/* ── SugarShop PWA Service Worker ──
   استراتژی‌ها:
   - صفحات (navigation): فقط شبکه. اگر شبکه نبود، صفحه آفلاین. HTML هرگز کش نمی‌شود؛
     چون صفحات سایت شخصی‌اند (سبد، سفارش، صورت‌حساب، داشبورد) و صفحهای که در «حالت اپ»
     رندر شدهاند نباید بعداً برای کاربر وب بازپخش شوند — علت اصلی «بعد از برگشتن از چاپ،
     صفحه حالت اپ بود».
   - استاتیک‌ها (css/js/img/font): کش اول — سرعت فوق‌العاده + کارکرد آفلاین
   - APIها: هرگز کش نمی‌شوند (داده باید همیشه تازه باشد)
 */
// بامپ نسخه: کش‌های قبلی کامل پاک می‌شوند. v1.2.0 = صفحه‌بندی هدر در PWA نصب‌شده و
// تفکیک قطعی حالت اپ/وب (نوار بالای اپ + اصلاح !important حالت standalone در pwa.css).
// بدون این بامپ، استاتیک‌های کش‌شده (css/js) ممکن بود تا یک بار بارگذاری دیگر قدیمی بمانند.
const VERSION = 'v1.2.0';
const STATIC_CACHE = `sugarshop-static-${VERSION}`;
const OFFLINE_URL = '/Pwa/Offline';

const PRECACHE = [
    OFFLINE_URL,
    '/Pwa/Icon?size=192',
    '/Pwa/Icon?size=512',
    '/Pwa/Icon?size=512&maskable=true',
    '/css/pwa.css',
    '/js/pwa.js',
    '/lib/bootstrap/dist/css/bootstrap.rtl.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/sweetalert2/sweetalert2.min.css',
    '/lib/sweetalert2/sweetalert2.min.js',
    '/fonts/Vazir/Vazirmatn-Regular.woff2',
    '/fonts/Vazir/Vazirmatn-Bold.woff2',
    '/fonts/Vazir/Vazirmatn-Medium.woff2',
    '/fonts/Vazir/Vazir.woff2',
    '/images/SORANSOFT_LOGO.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then(cache => cache.addAll(PRECACHE))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keep = new Set([STATIC_CACHE]);
        const names = await caches.keys();
        await Promise.all(names.filter(n => !keep.has(n)).map(n => caches.delete(n)));
        await self.clients.claim();
        // اطلاع به کلاینت‌ها که نسخه جدید فعال شد
        const clients = await self.clients.matchAll({ type: 'window' });
        clients.forEach(c => c.postMessage({ type: 'SW_UPDATED', version: VERSION }));
    })());
});

self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;      // منابع خارجی: بدون دخالت
    if (url.pathname.startsWith('/api/')) return;          // API همیشه زنده
    if (url.pathname.startsWith('/hangfire')) return;

    // ۱) ناوبری صفحات: همیشه از شبکه؛ در نبود شبکه فقط صفحه آفلاین.
    //    (کش‌کردن HTML صفحات حذف شد؛ صفحات شخصی/اپ‌محور نباید بازپخش شوند)
    if (req.mode === 'navigate') {
        event.respondWith(fetch(req).catch(async () => {
            const cache = await caches.open(STATIC_CACHE);
            return (await cache.match(OFFLINE_URL)) || Response.error();
        }));
        return;
    }

    // ۲) استاتیک‌ها: کش اول (سرعت + آفلاین)، به‌روزرسانی در پس‌زمینه
    const isStatic = /\.(css|js|png|jpg|jpeg|gif|webp|svg|ico|woff2?|ttf|mp4|webm)$/i.test(url.pathname)
        || url.pathname.startsWith('/lib/')
        || url.pathname.startsWith('/css/')
        || url.pathname.startsWith('/js/')
        || url.pathname.startsWith('/fonts/')
        || url.pathname.startsWith('/images/')
        || url.pathname.startsWith('/media/')
        || url.pathname.startsWith('/icons/');

    if (isStatic) {
        event.respondWith((async () => {
            const cache = await caches.open(STATIC_CACHE);
            const cached = await cache.match(req);
            const fetchAndCache = fetch(req).then(res => {
                if (res && res.ok) cache.put(req, res.clone());
                return res;
            }).catch(() => undefined);
            return cached || (await fetchAndCache) || Response.error();
        })());
    }
});
