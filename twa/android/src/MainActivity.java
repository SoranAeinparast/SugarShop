package ir.soransoftpro.pastry;

import android.app.Activity;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.graphics.Color;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.NetworkInfo;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.view.WindowInsets;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.webkit.WebChromeClient;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.net.http.SslError;
import android.webkit.SslErrorHandler;
import android.webkit.ValueCallback;
import android.content.ActivityNotFoundException;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;

/**
 * TWA-سبک شل اندروید برای فروشگاه شیرینی‌سرا
 * بدون وابستگی به androidx یا کتابخانه بیرونی — فقط WebView استوک.
 * URL را از asset "config.json" می‌خواند تا بدون بازسازی جاوا قابل تغییر باشد.
 *
 * پایداری: اگر راه‌اندازی WebView (شایع‌ترین علت «اپ اجرا نمی‌شود» — غیرفعال/قدیمی
 * بودن Android System WebView) یا هر بخش دیگری از start با خطا مواجه شود، اپ
 * به‌جای بستن، صفحه‌ی تشخیص بومی نشان می‌دهد: علت واقعی + راهنمای فارسی.
 * خطاهای پیش‌بینی‌نشده هم در فایل ثبت می‌شوند و در اجرای بعدی تا اولین رندر موفق
 * نمایش داده می‌شوند تا برای پشتیبانی قابل ارسال باشند.
 */
public class MainActivity extends Activity {

    private WebView webView;
    private String startUrl;
    private String expectedHost;
    private ValueCallback<Uri[]> filePathCallback;
    private static final int FILE_CHOOSER_REQUEST = 1001;

    /** رنگ پشت نوارهای سیستم — هم‌رنگ هدر سایت، تا نوار وضعیت بخشی از هدر به‌نظر برسد */
    private static final String SYSTEM_BAR_COLOR = "#6D3410";
    /** حداکثر زمان نمایش اسپلش حتی اگر سایت کند بود */
    private static final long SPLASH_TIMEOUT_MS = 8000;

    // ── اسپلش ──
    private View splashView;
    private boolean splashDismissed = false;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final Runnable splashTimeout = this::dismissSplash;

    // ── صفحه‌ی آفلاین ──
    private LinearLayout offlineScreen;
    private TextView txtErrorDetail;
    private boolean isOfflineShown = false;
    private ConnectivityManager connectivityManager;
    private NetworkCallback21 networkCallback;
    private ConnectivityReceiver legacyReceiver;

    // ── صفحه‌ی تشخیص خطا ──
    private LinearLayout diagScreen;
    private TextView txtDiagHint, txtDiagDetail;
    private static final String CRASH_FILE = "crash_log.txt";
    private static final long CRASH_LOG_MAX_AGE_MS = 12L * 60 * 60 * 1000; // ۱۲ ساعت

    // ── قفل بیومتریک اپ ──
    private LinearLayout lockGate;
    private TextView txtLockHint;
    private android.content.SharedPreferences lockPrefs;
    private boolean lockEnabled = false;
    private boolean unlockPending = false; // در حال نمایش پرامپت بیومتریک
    private boolean unlockedOnce = false;  // در این اجرای اپ، حداقل یک‌بار باز شده — پرامپت دوم خودکار ممنوع
    private static final int BIOMETRIC_CONFIRM_REQ = 1002; // fallback/API 23–27

    // ── دریافت خودکار کد OTP از پیامک ──
    private SmsOtpReceiver smsOtpReceiver;
    private volatile String smsOtpCode = null;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // ── خواندن config.json از assets ──
        String cfgUrl = "https://sweets.soransoftpro.ir";
        try {
            java.io.InputStream is = getAssets().open("config.json");
            byte[] buf = new byte[is.available()];
            is.read(buf);
            is.close();
            String json = new String(buf, "UTF-8");
            java.util.regex.Matcher m = java.util.regex.Pattern
                    .compile("\"url\"\\s*:\\s*\"([^\"]+)\"").matcher(json);
            if (m.find()) cfgUrl = m.group(1);
        } catch (Exception ignored) { }

        try {
            java.net.URI u = java.net.URI.create(cfgUrl);
            expectedHost = u.getHost();
        } catch (Exception e) {
            expectedHost = "sweets.soransoftpro.ir";
        }
        startUrl = cfgUrl;

        // ── ثبت خطاهای پیش‌بینی‌نشده؛ اجرای بعدی علت را نشان می‌دهد ──
        final Thread.UncaughtExceptionHandler systemHandler = Thread.getDefaultUncaughtExceptionHandler();
        Thread.setDefaultUncaughtExceptionHandler((t, e) -> {
            try { saveCrashLog(e); } catch (Exception ignored) { }
            if (systemHandler != null) systemHandler.uncaughtException(t, e);
        });

        boolean initOk = false;
        FrameLayout container = new FrameLayout(this);

        /*
         * ═══ راه‌اندازی امن ═══
         * هر خرابی (به‌خصوص ساخت WebView) به‌جای بستن ناگهانی اپ، صفحه‌ی تشخیص نشان می‌دهد.
         */
        try {
            // ── WebView + لایه‌ی اسپلش + لایه‌ی آفلاین + لایه‌ی تشخیص خطا ──
            webView = new WebView(this);
            webView.setBackgroundColor(Color.parseColor("#FFF8F0"));
            container.addView(webView, new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT,
                    FrameLayout.LayoutParams.MATCH_PARENT));

            // اسپلش داخل اپ: همان تصویر پس‌زمینه‌ی پنجره + راهنمای لود
            splashView = LayoutInflater.from(this)
                    .inflate(R.layout.splash_screen, container, false);
            container.addView(splashView, new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT,
                    FrameLayout.LayoutParams.MATCH_PARENT));

            offlineScreen = (LinearLayout) LayoutInflater.from(this)
                    .inflate(R.layout.offline_screen, container, false);
            container.addView(offlineScreen, new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT,
                    FrameLayout.LayoutParams.MATCH_PARENT));

            // صفحه‌ی تشخیص خطا — بالاترین لایه؛ فقط هنگام خطا دیده می‌شود
            diagScreen = (LinearLayout) LayoutInflater.from(this)
                    .inflate(R.layout.diag_screen, container, false);
            txtDiagHint = diagScreen.findViewById(R.id.txtDiagHint);
            txtDiagDetail = diagScreen.findViewById(R.id.txtDiagDetail);
            diagScreen.findViewById(R.id.btnDiagRetry).setOnClickListener(v -> {
                clearCrashLog();
                diagScreen.setVisibility(View.GONE);
                recreate();
            });
            container.addView(diagScreen, new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT,
                    FrameLayout.LayoutParams.MATCH_PARENT));

            // ── دروازه‌ی قفل بیومتریک — بالاترین لایه، فقط وقتی قفل فعال باشد دیده می‌شود ──
            lockGate = (LinearLayout) LayoutInflater.from(this)
                    .inflate(R.layout.lock_gate, container, false);
            txtLockHint = lockGate.findViewById(R.id.txtLockHint);
            lockGate.findViewById(R.id.btnLockUnlock).setOnClickListener(v -> showBiometricPrompt());
            lockGate.findViewById(R.id.btnLockDisable).setOnClickListener(v -> disableAppLock());
            container.addView(lockGate, new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT,
                    FrameLayout.LayoutParams.MATCH_PARENT));

            setContentView(container);

            txtErrorDetail = offlineScreen.findViewById(R.id.txtErrorDetail);
            Button btnRetry = offlineScreen.findViewById(R.id.btnRetry);
            btnRetry.setOnClickListener(v -> retryLoad());

            connectivityManager = (ConnectivityManager) getSystemService(Context.CONNECTIVITY_SERVICE);

            /*
             * ═══ رفع هم‌پوشانی هدر سایت با ساعت/باتری/آنتن ═══
             * targetSdk 35 روی Android 15 محتوا را edge-to-edge رسم می‌کند؛
             * به ریشه padding نوار وضعیت/ناچ (بالا) و ناوبری/کیبورد (پایین) می‌دهیم.
             */
            final View root = findViewById(android.R.id.content);
            root.setBackgroundColor(Color.parseColor(SYSTEM_BAR_COLOR));

            if (Build.VERSION.SDK_INT >= 30) {
                getWindow().setDecorFitsSystemWindows(false);
            } else {
                // اندروید ۶ تا ۱۰: فعال‌سازی edge-to-edge با فلگ‌های کلاسیک
                getWindow().getDecorView().setSystemUiVisibility(
                        View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                      | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                      | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
            }

            root.setOnApplyWindowInsetsListener((v, insets) -> {
                int top, bottom;
                if (Build.VERSION.SDK_INT >= 30) {
                    top = insets.getInsets(
                            WindowInsets.Type.statusBars()
                          | WindowInsets.Type.displayCutout()).top;
                    int nav = insets.getInsets(WindowInsets.Type.navigationBars()).bottom;
                    int ime = insets.getInsets(WindowInsets.Type.ime()).bottom;
                    bottom = Math.max(nav, ime); // کیبورد باز شود، فیلدها زیرش نمانند
                } else {
                    top = insets.getSystemWindowInsetTop();       // نوار وضعیت + ناچ
                    bottom = insets.getSystemWindowInsetBottom(); // ناوبری + کیبورد
                }
                if (v.getPaddingTop() != top || v.getPaddingBottom() != bottom) {
                    v.setPadding(0, top, 0, bottom);
                }
                return insets;
            });

            WebSettings s = webView.getSettings();
            s.setJavaScriptEnabled(true);
            s.setDomStorageEnabled(true);
            s.setDatabaseEnabled(true);
            s.setLoadWithOverviewMode(true);
            s.setUseWideViewPort(true);
            s.setMediaPlaybackRequiresUserGesture(false);
            s.setSupportMultipleWindows(false);
            s.setAllowFileAccess(false);
            s.setAllowContentAccess(false);
            s.setCacheMode(WebSettings.LOAD_DEFAULT);

            // Like TWA: keep users inside the app for our domain, hand off others.
            webView.setWebViewClient(new WebViewClient() {
                @Override
                public boolean shouldOverrideUrlLoading(WebView view, String url) {
                    Uri uri = Uri.parse(url);
                    String host = uri.getHost() == null ? "" : uri.getHost();
                    if (expectedHost != null && expectedHost.equals(host)) {
                        return false; // load in-app
                    }
                    // ═══ درگاه‌های پرداخت داخل اپ باز می‌شوند ═══
                    // اگر درگاه به مرورگر بیرونی دستیاری شود، پس از پرداخت، کاربر به اپ
                    // برنمی‌گردد و صفحه‌ی نتیجه در مرورگر باز می‌شود. با باز نگه داشتن درگاه
                    // در همین WebView، مسیر کامل پرداخت ← تأیید ← بازگشت به سفارش‌ها در اپ می‌ماند.
                    if (isPaymentGatewayHost(host)) {
                        return false; // load in-app
                    }
                    try {
                        startActivity(new Intent(Intent.ACTION_VIEW, uri)); // browser
                    } catch (ActivityNotFoundException ignored) { }
                    return true;
                }

                @Override
                public void onPageCommitVisible(WebView view, String url) {
                    // اولین فریم سایت رندر شد — اسپلش کنار می‌رود و خطای ثبت‌شده‌ی قبلی پاک می‌شود
                    dismissSplash();
                    hideDiag();
                }

                @Override
                public void onReceivedError(WebView view, int errorCode, String description, String failingUrl) {
                    // API < 23: همه‌ی خطاها (شامل ساب‌منابع) — فقط خطاهای اصلی را می‌خواهیم
                    if (failingUrl != null && failingUrl.equals(view.getUrl())
                            && isNetworkRelated(errorCode)) {
                        dismissSplash();
                        showOffline(describeError(errorCode, description));
                    }
                }

                @Override
                public void onReceivedError(WebView view, android.webkit.WebResourceRequest request,
                                            android.webkit.WebResourceError error) {
                    // API 23+: فقط فریم اصلی — خطای منابع فرعی (عکس و...) اپ را آفلاین نشان نمی‌دهد
                    if (request.isForMainFrame() && isNetworkRelated(error.getErrorCode())) {
                        dismissSplash();
                        showOffline(describeError(error.getErrorCode(),
                                error.getDescription() != null ? error.getDescription().toString() : ""));
                    }
                }

                @Override
                public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError error) {
                    // هرگز خطاهای SSL را نادیده نمی‌گیریم — امنیت کاربر اول است.
                    handler.cancel();
                    dismissSplash();
                    showOffline(getString(R.string.offline_ssl_title));
                }
            });

            // پل JS + نشانه UA تا سایت بفهمد داخل اپ است (بنر نصب مخفی شود)
            smsOtpReceiver = new SmsOtpReceiver();
            webView.addJavascriptInterface(new BackHandler(MainActivity.this, smsOtpReceiver), "SugarShopApp");
            String ua = s.getUserAgentString();
            if (ua != null && !ua.contains("SugarShopApp/")) {
                s.setUserAgentString(ua + " SugarShopApp/1.4");
            }

            webView.setWebChromeClient(new WebChromeClient() {
                @Override
                public boolean onShowFileChooser(WebView wv, ValueCallback<Uri[]> callback,
                                                 FileChooserParams params) {
                    if (filePathCallback != null) filePathCallback.onReceiveValue(null);
                    filePathCallback = callback;
                    try {
                        Intent intent = params.createIntent();
                        startActivityForResult(intent, FILE_CHOOSER_REQUEST);
                    } catch (ActivityNotFoundException e) {
                        filePathCallback = null;
                        return false;
                    }
                    return true;
                }
            });

            initOk = true;
        } catch (Throwable t) {
            // اپ بسته نمی‌شود: صفحه‌ی تشخیص با علت واقعی نشان داده می‌شود
            try { if (container.getParent() == null) setContentView(container); } catch (Exception ignored) { }
            showDiag(t);
        }

        if (initOk) {
            // ═══ بازگشت از درگاه پرداخت: اگر با intent حاوی url باز شده باشد، همان صفحه در اپ لود می‌شود ═══
            String intentUrl = (getIntent() != null) ? getIntent().getStringExtra("url") : null;

            if (savedInstanceState != null) {
                webView.restoreState(savedInstanceState);
                // بازیابی فوری محتوا؛ اسپلش تا اولین commit دیده می‌شود
                if (intentUrl != null && isOurUrl(intentUrl)) webView.loadUrl(intentUrl);
            } else if (intentUrl != null && isOurUrl(intentUrl)) {
                webView.loadUrl(intentUrl);
            } else if (isOnline()) {
                webView.loadUrl(startUrl);
            } else {
                dismissSplash();
                showOffline(null);
            }

            // اگر اجرای قبلی خطای ثبت‌شده داشت، تا اولین رندر موفق همین اجرا نشان داده و پاک می‌شود
            showSavedCrash();

            // اهرم ایمنی: حتی اگر سایت خیلی کند بود، اسپلش حداکثر ۸ ثانیه می‌ماند
            mainHandler.postDelayed(splashTimeout, SPLASH_TIMEOUT_MS);

            // ═══ بازگشت خودکار: به محض وصل شدن اینترنت، صفحه دوباره بارگذاری می‌شود ═══
            registerNetworkWatchers();

            // ═══ قفل بیومتریک: اگر فعال باشد، اول ورود با اثر انگشت ═══
            lockPrefs = getSharedPreferences(getString(R.string.biometric_prefs), Context.MODE_PRIVATE);
            lockEnabled = lockPrefs.getBoolean(getString(R.string.biometric_prefs_key), false);
            if (lockEnabled) {
                applyLockGate(true);
            }
        }
    }

    // ─────────────────── قفل بیومتریک اپ ───────────────────

    /** آیا دستگاه قابلیت قفل بیومتریک دارد؟ — همان چندلایه‌ی پل JS (سنسور اثر انگشت حکم نهایی) */
    private boolean deviceHasBiometric() {
        if (Build.VERSION.SDK_INT >= 30) {
            try {
                android.hardware.biometrics.BiometricManager bm =
                        (android.hardware.biometrics.BiometricManager) getSystemService(Context.BIOMETRIC_SERVICE);
                if (bm != null && bm.canAuthenticate(
                        android.hardware.biometrics.BiometricManager.Authenticators.BIOMETRIC_WEAK)
                        == android.hardware.biometrics.BiometricManager.BIOMETRIC_SUCCESS) return true;
            } catch (Throwable ignored) { }
        }
        try {
            if (Build.VERSION.SDK_INT >= 28) {
                android.hardware.biometrics.BiometricManager bm =
                        (android.hardware.biometrics.BiometricManager) getSystemService(Context.BIOMETRIC_SERVICE);
                if (bm != null && bm.canAuthenticate() == android.hardware.biometrics.BiometricManager.BIOMETRIC_SUCCESS)
                    return true;
            }
        } catch (Throwable ignored) { }
        try {
            if (Build.VERSION.SDK_INT >= 23) {
                android.hardware.fingerprint.FingerprintManager fm =
                        (android.hardware.fingerprint.FingerprintManager) getSystemService(Context.FINGERPRINT_SERVICE);
                if (fm != null && fm.isHardwareDetected() && fm.hasEnrolledFingerprints()) return true;
            }
        } catch (Throwable ignored) { }
        try {
            android.app.KeyguardManager km = (android.app.KeyguardManager) getSystemService(Context.KEYGUARD_SERVICE);
            return km != null && km.isKeyguardSecure();
        } catch (Throwable ignored) { }
        return false;
    }

    /** نمایش/پنهان کردن دروازه‌ی قفل روی همه‌ی لایه‌ها */
    private void applyLockGate(boolean locked) {
        if (lockGate == null) return;
        lockGate.setVisibility(locked ? View.VISIBLE : View.GONE);
        if (locked) {
            // اسپلش بلافاصله کنار برود؛ قفل جایش را می‌گیرد
            dismissSplash();
            if (!unlockedOnce) showBiometricPrompt(); // فقط بار اول؛ اگر قبلاً در همین اجرا باز شده، دوباره نپرس
        }
    }

    /** پرامپت بیومتریک — API 28+ بیومتریک‌پرامپت سیستمی، قدیمی‌تر KeyguardManager */
    private void showBiometricPrompt() {
        if (!deviceHasBiometric()) {
            txtLockHint.setText(R.string.biometric_fail);
            return;
        }
        if (unlockPending) return;
        unlockPending = true;

        if (Build.VERSION.SDK_INT >= 28) {
            try {
                android.hardware.biometrics.BiometricPrompt.Builder b =
                        new android.hardware.biometrics.BiometricPrompt.Builder(this)
                                .setTitle(getString(R.string.biometric_title))
                                .setSubtitle(getString(R.string.biometric_hint));
                if (Build.VERSION.SDK_INT >= 29) {
                    // API 29+: اثر انگشت (یا credential دستگاه) — دستگاه‌های OEM که پرامپت را می‌شناسند
                    b.setAllowedAuthenticators(
                            android.hardware.biometrics.BiometricManager.Authenticators.BIOMETRIC_WEAK
                          | android.hardware.biometrics.BiometricManager.Authenticators.DEVICE_CREDENTIAL);
                } else {
                    // API 28: فقط setDeviceCredentialAllowed مجاز است (setAllowedAuthenticators ندارد)
                    b.setDeviceCredentialAllowed(true);
                }
                android.hardware.biometrics.BiometricPrompt prompt = b.build();
                java.util.concurrent.Executor ex = ContextCompat_main();
                prompt.authenticate(new android.os.CancellationSignal(), ex,
                        new android.hardware.biometrics.BiometricPrompt.AuthenticationCallback() {
                            @Override
                            public void onAuthenticationSucceeded(
                                    android.hardware.biometrics.BiometricPrompt.AuthenticationResult result) {
                                unlockPending = false;
                                runOnUiThread(() -> onUnlockSuccess());
                            }
                            @Override
                            public void onAuthenticationError(int code, CharSequence errString) {
                                unlockPending = false;
                                runOnUiThread(() -> {
                                    if (code == 1 /* USER_CANCELED */ || code == 2 /* NEGATIVE_BUTTON */) {
                                        txtLockHint.setText(R.string.biometric_hint); // بماند تا دکمه بزند
                                    } else {
                                        txtLockHint.setText(errString != null ? errString.toString() : getString(R.string.biometric_fail));
                                    }
                                });
                            }
                            @Override
                            public void onAuthenticationFailed() {
                                runOnUiThread(() -> txtLockHint.setText(R.string.biometric_fail));
                            }
                        });
                return;
            } catch (Throwable t) {
                unlockPending = false; // به fallback می‌رود
            }
        }

        // API 23 تا 27: تأیید صفحه‌قفل دستگاه (اثر انگورتا/الگو/PIN)
        try {
            android.app.KeyguardManager km = (android.app.KeyguardManager) getSystemService(Context.KEYGUARD_SERVICE);
            Intent i = km.createConfirmDeviceCredentialIntent(
                    getString(R.string.biometric_title), getString(R.string.biometric_hint));
            startActivityForResult(i, BIOMETRIC_CONFIRM_REQ);
        } catch (Exception e) {
            unlockPending = false;
            txtLockHint.setText(R.string.biometric_fail);
        }
    }

    private java.util.concurrent.Executor ContextCompat_main() {
        // اجرا روی main thread — بدون androidx
        return java.util.concurrent.Executors.newSingleThreadExecutor();
    }

    private void onUnlockSuccess() {
        unlockedOnce = true; // دیگر در این اجرا پرامپت خودکار تکرار نمی‌شود
        txtLockHint.setText(R.string.biometric_ok);
        mainHandler.postDelayed(() -> applyLockGate(false), 350);
    }

    private void disableAppLock() {
        lockPrefs.edit().putBoolean(getString(R.string.biometric_prefs_key), false).apply();
        lockEnabled = false;
        applyLockGate(false);
        android.widget.Toast.makeText(this, getString(R.string.biometric_disabled_by_user),
                android.widget.Toast.LENGTH_LONG).show();
    }

    // ─────────────────────── اسپلش ───────────────────────

    private void dismissSplash() {
        if (splashDismissed || splashView == null) return;
        splashDismissed = true;
        mainHandler.removeCallbacks(splashTimeout);
        splashView.animate()
                .alpha(0f)
                .setDuration(280L)
                .withEndAction(() -> {
                    if (splashView != null) {
                        splashView.setVisibility(View.GONE);
                        // پس‌زمینه‌ی پنجره دیگر لازم نیست؛ حافظه و دررفتگی اسکرول آزاد شود
                        getWindow().setBackgroundDrawableResource(android.R.color.transparent);
                    }
                })
                .start();
    }

    // ─────────────────────── صفحه‌ی آفلاین ───────────────────────

    private void showOffline(String detail) {
        isOfflineShown = true;
        offlineScreen.setVisibility(View.VISIBLE);
        webView.setVisibility(View.INVISIBLE); // صفحه‌ی خطای خام WebView پنهان بماند
        if (detail != null && !detail.trim().isEmpty()) {
            txtErrorDetail.setText(detail);
            txtErrorDetail.setVisibility(View.VISIBLE);
        } else {
            txtErrorDetail.setVisibility(View.GONE);
        }
    }

    private void hideOffline() {
        isOfflineShown = false;
        offlineScreen.setVisibility(View.GONE);
        webView.setVisibility(View.VISIBLE);
        txtErrorDetail.setVisibility(View.GONE);
    }

    private void retryLoad() {
        if (!isOnline()) {
            android.widget.Toast.makeText(this,
                    getString(R.string.offline_still_no_internet),
                    android.widget.Toast.LENGTH_SHORT).show();
            return;
        }
        hideOffline();
        webView.setVisibility(View.VISIBLE);
        webView.reload();
    }

    private boolean isOnline() {
        if (connectivityManager == null) return true; // نامشخص؟ اجازه‌ی تلاش بده
        if (Build.VERSION.SDK_INT >= 23) {
            Network n = connectivityManager.getActiveNetwork();
            if (n == null) return false;
            NetworkCapabilities c = connectivityManager.getNetworkCapabilities(n);
            return c != null && c.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET);
        }
        @SuppressWarnings("deprecation")
        NetworkInfo info = connectivityManager.getActiveNetworkInfo();
        return info != null && info.isConnected();
    }

    private boolean isNetworkRelated(int code) {
        switch (code) {
            case WebViewClient.ERROR_HOST_LOOKUP:          // DNS
            case WebViewClient.ERROR_CONNECT:              // اتصال TCP
            case WebViewClient.ERROR_TIMEOUT:              // سرور پاسخ نداد
            case WebViewClient.ERROR_UNKNOWN:              // اغلب «internet may be down»
            case WebViewClient.ERROR_FAILED_SSL_HANDSHAKE:
                return true;
            default:
                return false;
        }
    }

    private String describeError(int code, String raw) {
        if (code == WebViewClient.ERROR_TIMEOUT) return getString(R.string.offline_server_down);
        if (code == WebViewClient.ERROR_HOST_LOOKUP) return getString(R.string.offline_dns_fail);
        return raw != null ? raw : "";
    }

    // ─────────────────── صفحه‌ی تشخیص خطا ───────────────────

    /** علت واقعی خرابی راه‌اندازی — با تشخیص ویژه‌ی مشکل Android System WebView */
    private void showDiag(Throwable t) {
        if (diagScreen == null) return;
        boolean webviewIssue = false;
        String msg = t == null ? "" : String.valueOf(t.getMessage());
        if (t != null) {
            String cls = t.getClass().getName();
            String low = msg == null ? "" : msg.toLowerCase();
            if (cls.contains("WebView") || low.contains("webview")) webviewIssue = true;
            for (StackTraceElement el : t.getStackTrace()) {
                String cn = el.getClassName();
                if (cn.startsWith("android.webkit") || cn.contains("WebView")) { webviewIssue = true; break; }
            }
        }
        txtDiagHint.setText(webviewIssue ? R.string.diag_hint_webview : R.string.diag_hint_other);
        txtDiagDetail.setText(t == null ? "نامشخص" : Log.getStackTraceString(t));
        diagScreen.setVisibility(View.VISIBLE);
    }

    /** خطای ثبت‌شده از اجرای قبلی — تا اولین رندر موفق نمایش داده و پاک می‌شود */
    private void showSavedCrash() {
        try {
            File f = new File(getFilesDir(), CRASH_FILE);
            if (!f.exists()) return;
            long age = System.currentTimeMillis() - f.lastModified();
            if (age > CRASH_LOG_MAX_AGE_MS) { clearCrashLog(); return; }

            StringBuilder sb = new StringBuilder();
            BufferedReader r = new BufferedReader(new InputStreamReader(new FileInputStream(f), "UTF-8"));
            String line;
            while ((line = r.readLine()) != null) sb.append(line).append('\n');
            r.close();

            String text = sb.toString().trim();
            if (!text.isEmpty() && diagScreen != null) {
                txtDiagHint.setText(R.string.diag_hint_other);
                txtDiagDetail.setText(text);
                diagScreen.setVisibility(View.VISIBLE);
            }
        } catch (Exception ignored) { }
    }

    private void hideDiag() {
        if (diagScreen != null && diagScreen.getVisibility() == View.VISIBLE) {
            diagScreen.setVisibility(View.GONE);
            clearCrashLog();
        }
    }

    private void saveCrashLog(Throwable t) {
        try {
            File f = new File(getFilesDir(), CRASH_FILE);
            PrintWriter w = new PrintWriter(new OutputStreamWriter(new FileOutputStream(f), "UTF-8"));
            w.println("app=1.4.1  android=" + Build.VERSION.SDK_INT
                    + "  device=" + Build.MANUFACTURER + " " + Build.MODEL);
            w.println(t == null ? "نامشخص" : Log.getStackTraceString(t));
            w.close();
        } catch (Exception ignored) { }
    }

    private void clearCrashLog() {
        try { new File(getFilesDir(), CRASH_FILE).delete(); } catch (Exception ignored) { }
    }

    // ─────────────── پایش شبکه برای بازگشت خودکار ───────────────

    private void registerNetworkWatchers() {
        if (Build.VERSION.SDK_INT >= 24) {
            networkCallback = new NetworkCallback21();
            try {
                connectivityManager.registerDefaultNetworkCallback(networkCallback);
            } catch (Exception ignored) { }
        } else {
            // اندروید ۶/۷: از CONNECTIVITY_ACTION استفاده می‌کنیم
            legacyReceiver = new ConnectivityReceiver();
            IntentFilter f = new IntentFilter(ConnectivityManager.CONNECTIVITY_ACTION);
            registerReceiver(legacyReceiver, f);
        }
    }

    /** API 24+: هر بار شبکه‌ی فعال «متصل» شود، اگر صفحه‌ی آفلاین دیده می‌شود، رفرش کن */
    private class NetworkCallback21 extends ConnectivityManager.NetworkCallback {
        private boolean lastAvailable = false;

        @Override
        public void onAvailable(Network network) {
            if (!lastAvailable && isOfflineShown) {
                runOnUiThread(() -> {
                    if (isOfflineShown && isOnline()) retryLoad();
                });
            }
            lastAvailable = true;
        }

        @Override
        public void onLost(Network network) {
            lastAvailable = false;
        }
    }

    /** اندروید ۶/۷: پخش تغییر وضعیت اتصال */
    private class ConnectivityReceiver extends BroadcastReceiver {
        @Override
        public void onReceive(Context context, Intent intent) {
            if (isOfflineShown && isOnline()) {
                runOnUiThread(() -> { if (isOfflineShown) retryLoad(); });
            }
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == FILE_CHOOSER_REQUEST) {
            Uri[] results = null;
            if (resultCode == RESULT_OK && data != null && data.getData() != null) {
                results = new Uri[]{ data.getData() };
            }
            if (filePathCallback != null) {
                filePathCallback.onReceiveValue(results);
                filePathCallback = null;
            }
            return;
        }
        if (requestCode == BIOMETRIC_CONFIRM_REQ) {
            unlockPending = false;
            if (resultCode == RESULT_OK) {
                onUnlockSuccess();
            } else {
                txtLockHint.setText(R.string.biometric_hint);
            }
            return;
        }
        super.onActivityResult(requestCode, resultCode, data);
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        if (webView != null) webView.saveState(outState);
    }

    @Override
    protected void onResume() {
        super.onResume();
        // اگر کاربر از تنظیمات وای‌فای برگشت، تلاش مجدد خودکار
        if (isOfflineShown && isOnline()) retryLoad();
        // قفل فعال باشد و هنوز باز نشده → دوباره بپرس (فقط تا اولین باز شدن موفق همین اجرا)
        if (lockEnabled && !unlockedOnce && lockGate != null
                && lockGate.getVisibility() == View.VISIBLE && !unlockPending) {
            showBiometricPrompt();
        }
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        // بازگشت از درگاه پرداخت: اپ همین حالا باز است؛ فقط صفحه‌ی مقصد را لود کن
        if (intent != null && webView != null) {
            String url = intent.getStringExtra("url");
            if (url != null && isOurUrl(url)) {
                runOnUiThread(() -> webView.loadUrl(url));
            }
        }
    }

    private boolean isOurUrl(String url) {
        try {
            Uri u = Uri.parse(url);
            return expectedHost != null && expectedHost.equals(u.getHost()) && "https".equals(u.getScheme());
        } catch (Exception e) {
            return false;
        }
    }

    /** آیا این هاست، درگاه پرداخت است؟ (درگاه‌ها داخل اپ باز می‌شوند تا بازگشت به اپ ممکن باشد) */
    private static boolean isPaymentGatewayHost(String host) {
        if (host == null) return false;
        String h = host.toLowerCase();
        return h.equals("gateway.zibal.ir") || h.endsWith(".zibal.ir")
            || h.equals("www.zarinpal.com") || h.equals("zarinpal.com")
            || h.endsWith(".zarinpal.com")
            || h.equals("sandbox.zarinpal.com");
    }

    @Override
    public void onBackPressed() {
        if (lockEnabled && lockGate != null && lockGate.getVisibility() == View.VISIBLE) {
            return; // قفل: بازگشت بسته شود؛ کاربر اثر انگورتا بزند یا دکمه
        }
        if (diagScreen != null && diagScreen.getVisibility() == View.VISIBLE) {
            return; // دکمه‌ی بازگشت روی صفحه‌ی تشخیص کاری نمی‌کند؛ کاربر «تلاش مجدد» را می‌زند
        }
        if (isOfflineShown) {
            if (isOnline()) retryLoad();
            return;
        }
        if (webView != null && webView.canGoBack()) {
            webView.goBack();
        } else {
            super.onBackPressed();
        }
    }

    @Override
    protected void onDestroy() {
        mainHandler.removeCallbacks(splashTimeout);
        if (smsOtpReceiver != null) smsOtpReceiver.stop(this);
        try {
            if (networkCallback != null && connectivityManager != null) {
                connectivityManager.unregisterNetworkCallback(networkCallback);
            }
            if (legacyReceiver != null) {
                unregisterReceiver(legacyReceiver);
            }
        } catch (Exception ignored) { }
        if (webView != null) {
            webView.destroy();
        }
        super.onDestroy();
    }
}
