/* ── SugarShop PWA Service Worker ──
   استراتژی‌ها:
   - صفحات (navigation): شبکه اول، در قطعی اینترنت صفحه آفلاین
   - استاتیک‌ها (css/js/img/font): کش اول — سرعت فوق‌العاده + کارکرد آفلاین
   - APIها: هرگز کش نمی‌شوند (داده باید همیشه تازه باشد)
*/
const VERSION = 'v1.0.0';
const STATIC_CACHE = `sugarshop-static-${VERSION}`;
const PAGES_CACHE = `sugarshop-pages-${VERSION}`;
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
        const keep = new Set([STATIC_CACHE, PAGES_CACHE]);
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

    // ۱) ناوبری صفحات: شبکه اول + صفحه آفلاین به‌عنوان جانشین
    if (req.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                const fresh = await fetch(req);
                const cache = await caches.open(PAGES_CACHE);
                cache.put(req, fresh.clone());
                return fresh;
            } catch {
                const cache = await caches.open(PAGES_CACHE);
                const cached = await cache.match(req);
                if (cached) return cached;
                const offline = await cache.match(OFFLINE_URL);
                return offline || Response.error();
            }
        })());
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
