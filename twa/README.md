╔══════════════════════════════════════════════════════════════╗
   SugarShop TWA — Android shell (build once, update never)
╚══════════════════════════════════════════════════════════════╝

This folder contains a minimal, dependency-free Android WebView shell
(TWA-style) that opens https://pastry.soransoftpro.ir fullscreen.
It does NOT contain any shop logic — all logic lives in the ASP.NET site.

────────────────────────────────────────────────────────────
 1) Prerequisites (already present on this PC)
────────────────────────────────────────────────────────────
  • Android SDK ......... C:\Program Files (x86)\Android\android-sdk
                          (build-tools 35.0.0, platform android-35)
  • JDK 21 .............. C:\Program Files\Android\Android Studio\jbr
  • PowerShell .......... for build-apk.ps1

────────────────────────────────────────────────────────────
 2) Build the APK   ✅ ALREADY DONE — app-release-signed.apk exists
────────────────────────────────────────────────────────────
  powershell -ExecutionPolicy Bypass -File build-apk.ps1

  Output:  twa\app-release-signed.apk  (+ twa\keystore.jks kept local)
  The script also prints the SHA-256 fingerprint of the signing key.

  ⚠ BEFORE REBUILDING for an update: bump android:versionCode and
    android:versionName in twa\android\AndroidManifest.xml.

────────────────────────────────────────────────────────────
 3) Wire the fingerprint into the site   ✅ ALREADY DONE
────────────────────────────────────────────────────────────
  The fingerprint 74:1D:66:AE:...:2D:9D is already set in
  SugarShop.Web\appsettings.json → Twa:Sha256Fingerprints.
  Just make sure it is also present in the HOST's appsettings.Production.json
  when publishing (or that appsettings.json ships with the publish):

      "Twa": {
        "PackageName": "ir.soransoftpro.pastry",
        "Sha256Fingerprints": [ "AB:CD:...:9F" ]
      }

  • Re-publish the site. Android then verifies ownership via
    https://pastry.soransoftpro.ir/Pwa/.well-known/assetlinks.json
    (implemented dynamically in PwaController) and the app runs
    fullscreen with NO browser bar.

────────────────────────────────────────────────────────────
 4) Distribute on your own site (no Play Store needed)   ✅ APK COPIED
────────────────────────────────────────────────────────────
  • The APK lives at SugarShop.Web\Resources\pastry-app.apk (OUTSIDE wwwroot)
    and is served ONLY through the tracked route:
      https://pastry.soransoftpro.ir/App/GetTheApp
    Every download is logged (AppDownloadLogs table) and shown in the admin
    dashboard card «📊 آمار دانلود اپلیکیشن». The download page button and
    twa\README links already point there — never serve the APK as a static
    file again.
  • After rebuilding: copy twa\app-release-signed.apk over
    SugarShop.Web\Resources\pastry-app.apk and update the AppVersion string
    in AppController.GetTheApp.
  • Branded landing page (QR code + direct download + install guide):
      https://pastry.soransoftpro.ir/App/Download
    Linked from the main navbar (📱 اپلیکیشن) and a footer QR chip.
  • QR files: SugarShop.Web\wwwroot\images\app-qr.svg / app-qr.png —
    regenerate after a domain change (python qrcode lib, one-time).
  • Users install by opening the APK and allowing
    "install from this source" once.

────────────────────────────────────────────────────────────
 5) Change app icon
────────────────────────────────────────────────────────────
  • Edit generate-icons.bat line LOGO=... to your logo, install
    ImageMagick once, run:
      generate-icons.bat path\to\logo.png
  • Rebuild the APK.

────────────────────────────────────────────────────────────
 6) Change the URL the app opens
────────────────────────────────────────────────────────────
  • Edit twa\android\assets\config.json  → rebuild APK.
  • (No Java recompile needed for a URL change.)

────────────────────────────────────────────────────────────
 7) Version history / changelog
────────────────────────────────────────────────────────────
  | Version  | versionCode | Changes |
  |----------|-------------|---------|
  | 1.4.1    | 6 | **CRITICAL FIX — app crashed on every launch** ("closed because this app has a bug"): the manifest was missing `ACCESS_NETWORK_STATE`, so `ConnectivityManager` threw a SecurityException during startup. Also carries 1.4.0's changes below. |
  | 1.4.0    | 5 | Self-diagnosing startup: any init failure (e.g. disabled/outdated Android System WebView) shows a native diagnostic screen with the real cause + Persian guidance instead of silently crashing; unexpected errors are logged to a crash file and shown on next launch until first successful render. |
  | 1.3.0    | 4 | Branded splash screen: cold-start window background shows the store logo on cream (no white flash), in-app splash overlay stays until the site's first paint, fades out (280 ms), 8 s safety timeout. Regenerate splash art with twa\make-splash-logo.ps1 after changing the store logo. |
  | 1.2.0    | 3 | Friendly native offline screen (store branding + retry button) replaces the raw WebView error page. Auto-reloads the moment connectivity returns; detects DNS/TCP/timeout/server-down errors on the main frame only (a failed image never fakes an outage); SSL errors block safely instead of silently proceeding. |
  | 1.1.0    | 2 | Fixed site header hidden under status bar (clock/battery/signal): edge-to-edge insets now padded into the WebView — status bar is tinted the site's header color (#6D3410), keyboard no longer covers inputs. Also: network security config keeps the app working across host SSL certificate renewals. |
  | 1.0.0    | 1 | Initial release — fullscreen WebView, back-button nav, file upload, UA/JS-bridge app detection. |
  • Keystore is the app's identity — back it up! Losing it means
    users must uninstall/reinstall on future updates.
  • Version bumps: edit android:versionCode / versionName in
    twa\android\AndroidManifest.xml before rebuilding for updates.
  • iOS is NOT covered by TWA (Apple restriction) — iPhone users
    install the PWA via Safari share → Add to Home Screen.

## نسخه 1.6.0 (versionCode 8)
- برند اپ همگام با سایت: آیکون لانچر و اسپلش از لوگوی جدید «شیرینی سرای ایران» ساخته شد؛ نام اپ روی لانچر و اسپلش به‌روز شد.
- قفل بیومتریک: باز کردن اپ با اثر انگشت/قفل صفحه‌ی گوشی (BiometricPrompt روی اندروید ۹+، ConfirmCredential روی ۶–۸). فعال/غیرفعال‌سازی از پروفایل کاربر داخل سایت (کارت «امنیت اپلیکیشن» — فقط داخل اپ دیده می‌شود).
- ترمیم: فوتر سایت دیگر زیر نوار ناوبری پایین اپ پوشیده نمی‌شود (فاصله‌ی امن موبایل).
