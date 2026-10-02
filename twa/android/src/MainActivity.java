package ir.soransoftpro.pastry;

import android.app.Activity;
import android.app.AlertDialog;
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
import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.util.Locale;
import java.util.Collections;

import javax.crypto.Cipher;

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
    private volatile String currentTopLevelUrl;
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
    private BiometricTokenStore biometricTokenStore;
    private BackHandler appBridge;
    private boolean bridgeAttached = false;
    private boolean lockEnabled = false;
    private boolean unlockPending = false; // در حال نمایش پرامپت بیومتریک
    private boolean unlockedOnce = false;  // در این اجرای اپ، حداقل یک‌بار باز شده — پرامپت دوم خودکار ممنوع
    private boolean automaticPromptShown = false;
    private volatile boolean restorationPending = false;
    private boolean verifyingRestoredSession = false;
    private String biometricSessionLanding;
    private boolean accountLoginPending = false;
    private String pendingDestination;
    private long pendingEnrollmentExpiry;
    private android.os.CancellationSignal activeAuthSignal;
    private AlertDialog legacyBiometricDialog;
    private int authGeneration = 0;
    private final Runnable restorationTimeout = () -> failSessionRestore(false);
    private static final long SESSION_RESTORE_TIMEOUT_MS = 30000;
    private static final int SMS_PERMISSION_REQUEST = 1204;

    private enum BiometricAction { ENROLL, RESTORE }

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

        Uri configuredUri = Uri.parse(cfgUrl);
        expectedHost = "sweets.soransoftpro.ir";
        if (isSecureHttpUri(configuredUri)) {
            expectedHost = configuredUri.getHost().toLowerCase(Locale.US);
        } else {
            cfgUrl = "https://" + expectedHost;
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
            lockGate.findViewById(R.id.btnLockAccountLogin).setOnClickListener(v -> beginAccountLogin());
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
                    if (isBiometricSessionUrl(url) && !restorationPending) return true;
                    return routeNavigation(url);
                }

                @Override
                public boolean shouldOverrideUrlLoading(WebView view, android.webkit.WebResourceRequest request) {
                    String url = request.getUrl().toString();
                    if (isBiometricSessionUrl(url)
                            && (!request.isForMainFrame() || !restorationPending)) return true;
                    if (!request.isForMainFrame()) return false;
                    return routeNavigation(url);
                }

                @Override
                public android.webkit.WebResourceResponse shouldInterceptRequest(
                        WebView view, android.webkit.WebResourceRequest request) {
                    Uri uri = request.getUrl();
                    if (isBiometricSessionUrl(uri.toString())
                            && (!request.isForMainFrame() || !restorationPending)) {
                        return new android.webkit.WebResourceResponse("text/plain", "UTF-8", 403,
                                "Biometric restoration must be initiated by the app", Collections.emptyMap(),
                                new ByteArrayInputStream(new byte[0]));
                    }
                    boolean firstParty = isFirstPartyUri(uri);
                    boolean paymentGateway = isPaymentGatewayUri(uri);
                    boolean externalMainFrame = request.isForMainFrame() && !firstParty && !paymentGateway;
                    boolean paymentFrame = !request.isForMainFrame()
                            && isPaymentGatewayPage() && isSecureHttpUri(uri);
                    boolean externalHtmlFrame = !request.isForMainFrame() && !firstParty
                            && !paymentGateway && !paymentFrame && isHtmlDocumentRequest(request);
                    if (externalMainFrame || externalHtmlFrame) {
                        if (externalMainFrame) {
                            runOnUiThread(() -> {
                                if (restorationPending) failSessionRestore(true);
                                if (!accountLoginPending && isSafeExternalUri(uri)) openExternal(uri);
                            });
                        }
                        return new android.webkit.WebResourceResponse("text/plain", "UTF-8", 403,
                                "Blocked navigation", Collections.emptyMap(), new ByteArrayInputStream(new byte[0]));
                    }
                    return null;
                }

                @Override
                public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                    currentTopLevelUrl = url;
                    if (isOurUrl(url)) attachBridge(); else detachBridge();
                    if (!isOtpPageTrusted() && smsOtpReceiver != null) smsOtpReceiver.stop(MainActivity.this);
                    if (restorationPending && !isOurUrl(url)) failSessionRestore(true);
                }

                @Override
                public void onPageCommitVisible(WebView view, String url) {
                    // اولین فریم سایت رندر شد — اسپلش کنار می‌رود و خطای ثبت‌شده‌ی قبلی پاک می‌شود
                    dismissSplash();
                    hideDiag();
                    if (restorationPending) onSessionRestorePageCommitted(url);
                    if (accountLoginPending && isAuthenticatedProfileUrl(url)) finishAccountLogin();
                }

                @Override
                public void onReceivedError(WebView view, int errorCode, String description, String failingUrl) {
                    // API < 23: همه‌ی خطاها (شامل ساب‌منابع) — فقط خطاهای اصلی را می‌خواهیم
                    if (failingUrl != null && failingUrl.equals(view.getUrl())
                            && isNetworkRelated(errorCode)) {
                        dismissSplash();
                        showOffline(describeError(errorCode, description));
                        if (restorationPending) failSessionRestore(false);
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
                        if (restorationPending) failSessionRestore(false);
                    }
                }

                @Override
                public void onReceivedHttpError(WebView view, android.webkit.WebResourceRequest request,
                                                android.webkit.WebResourceResponse response) {
                    if (restorationPending && request.isForMainFrame()) {
                        int status = response.getStatusCode();
                        failSessionRestore(status == 401 || status == 403);
                    }
                }

                @Override
                public void onReceivedSslError(WebView view, SslErrorHandler handler, SslError error) {
                    // هرگز خطاهای SSL را نادیده نمی‌گیریم — امنیت کاربر اول است.
                    handler.cancel();
                    dismissSplash();
                    showOffline(getString(R.string.offline_ssl_title));
                    if (restorationPending) failSessionRestore(false);
                }
            });

            // پل JS + نشانه UA تا سایت بفهمد داخل اپ است (بنر نصب مخفی شود)
            smsOtpReceiver = new SmsOtpReceiver();
            appBridge = new BackHandler(MainActivity.this, smsOtpReceiver);
            attachBridge();
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
            biometricTokenStore = new BiometricTokenStore(this);
            lockEnabled = biometricTokenStore.isEnabled();
            pendingDestination = getIncomingUrl(getIntent());

            boolean restoredPage = !lockEnabled && savedInstanceState != null
                    && webView.restoreState(savedInstanceState) != null;
            if (restoredPage) {
                currentTopLevelUrl = webView.getUrl();
                if (isOurUrl(currentTopLevelUrl)) attachBridge();
            }
            if (lockEnabled) {
                applyLockGate(true);
            } else if (pendingDestination != null) {
                webView.loadUrl(pendingDestination);
            } else if (!restoredPage) {
                if (isOnline()) {
                    webView.loadUrl(startUrl);
                } else {
                    dismissSplash();
                    showOffline(null);
                }
            }

            // اگر اجرای قبلی خطای ثبت‌شده داشت، تا اولین رندر موفق همین اجرا نشان داده و پاک می‌شود
            showSavedCrash();

            // اهرم ایمنی: حتی اگر سایت خیلی کند بود، اسپلش حداکثر ۸ ثانیه می‌ماند
            mainHandler.postDelayed(splashTimeout, SPLASH_TIMEOUT_MS);

            // ═══ بازگشت خودکار: به محض وصل شدن اینترنت، صفحه دوباره بارگذاری می‌شود ═══
            registerNetworkWatchers();

        }
    }

    // ─────────────────── قفل بیومتریک و نشست حساب ───────────────────

    private boolean deviceHasBiometric() {
        try {
            if (Build.VERSION.SDK_INT >= 28) {
            android.hardware.biometrics.BiometricManager manager =
                        (android.hardware.biometrics.BiometricManager) getSystemService(Context.BIOMETRIC_SERVICE);
                if (manager != null) {
                    int result = Build.VERSION.SDK_INT >= 30
                            ? manager.canAuthenticate(android.hardware.biometrics.BiometricManager.Authenticators.BIOMETRIC_STRONG)
                            : manager.canAuthenticate();
                    if (result == android.hardware.biometrics.BiometricManager.BIOMETRIC_SUCCESS) return true;
                }
            }
        } catch (Throwable ignored) { }
        try {
            android.hardware.fingerprint.FingerprintManager manager =
                    (android.hardware.fingerprint.FingerprintManager) getSystemService(Context.FINGERPRINT_SERVICE);
            return manager != null && manager.isHardwareDetected() && manager.hasEnrolledFingerprints();
        } catch (Throwable ignored) { }
        return false;
    }

    private void applyLockGate(boolean locked) {
        if (lockGate == null) return;
        lockGate.setVisibility(locked ? View.VISIBLE : View.GONE);
        if (!locked) return;
        dismissSplash();
        if (!unlockedOnce && !automaticPromptShown) showBiometricPrompt();
    }

    private void showBiometricPrompt() {
        automaticPromptShown = true;
        if (unlockPending) return;
        if (biometricTokenStore == null || !biometricTokenStore.hasUsableToken()) {
            txtLockHint.setText(R.string.biometric_token_unavailable);
            return;
        }
        if (!deviceHasBiometric()) {
            txtLockHint.setText(R.string.biometric_token_unavailable);
            return;
        }
        try {
            startBiometricAuthentication(BiometricAction.RESTORE,
                    biometricTokenStore.newDecryptionCipher());
        } catch (Exception e) {
            cancelPendingAuthentication();
            biometricTokenStore.clearTokenKeepEnabled();
            txtLockHint.setText(R.string.biometric_token_unavailable);
        }
    }

    void beginBiometricEnrollment(long expiresAtMillis) {
        runOnUiThread(() -> {
            if (!isProfileSettingsPage() || expiresAtMillis <= System.currentTimeMillis()) {
                notifyProfileBiometricResult("enable", false);
                return;
            }
            if (!deviceHasBiometric()) {
                notifyProfileBiometricResult("enable", false);
                return;
            }
            if (unlockPending) {
                notifyProfileBiometricResult("enable", false);
                return;
            }
            try {
                pendingEnrollmentExpiry = expiresAtMillis;
                startBiometricAuthentication(BiometricAction.ENROLL,
                        biometricTokenStore.newEncryptionCipher());
            } catch (Exception e) {
                cancelPendingAuthentication();
                notifyProfileBiometricResult("enable", false);
            }
        });
    }

    private void startBiometricAuthentication(BiometricAction action, Cipher cipher) throws Exception {
        if (Build.VERSION.SDK_INT < 23 || cipher == null) throw new IllegalStateException("Biometric crypto unavailable");
        if (unlockPending) return;
        unlockPending = true;
        final int generation = ++authGeneration;
        activeAuthSignal = new android.os.CancellationSignal();

        if (Build.VERSION.SDK_INT >= 28) {
            android.hardware.biometrics.BiometricPrompt.Builder builder =
                    new android.hardware.biometrics.BiometricPrompt.Builder(this)
                            .setTitle(getString(R.string.biometric_title))
                            .setSubtitle(action == BiometricAction.ENROLL
                                    ? getString(R.string.biometric_prompt_enroll)
                                    : getString(R.string.biometric_hint))
                            .setNegativeButton(getString(R.string.biometric_cancel),
                                    command -> mainHandler.post(command), (dialog, which) -> { });
            if (Build.VERSION.SDK_INT >= 30) {
                builder.setAllowedAuthenticators(
                        android.hardware.biometrics.BiometricManager.Authenticators.BIOMETRIC_STRONG);
            }
            android.hardware.biometrics.BiometricPrompt prompt = builder.build();
            prompt.authenticate(new android.hardware.biometrics.BiometricPrompt.CryptoObject(cipher),
                    activeAuthSignal, command -> mainHandler.post(command),
                    new android.hardware.biometrics.BiometricPrompt.AuthenticationCallback() {
                        @Override
                        public void onAuthenticationSucceeded(
                                android.hardware.biometrics.BiometricPrompt.AuthenticationResult result) {
                            android.hardware.biometrics.BiometricPrompt.CryptoObject crypto = result.getCryptoObject();
                            onBiometricAuthenticated(generation, action,
                                    crypto == null ? null : crypto.getCipher());
                        }

                        @Override
                        public void onAuthenticationError(int code, CharSequence message) {
                            onBiometricError(generation, action, message);
                        }

                        @Override
                        public void onAuthenticationFailed() {
                            runOnUiThread(() -> {
                                if (generation == authGeneration && txtLockHint != null)
                                    txtLockHint.setText(R.string.biometric_fail);
                            });
                        }
                    });
            return;
        }

        android.hardware.fingerprint.FingerprintManager manager =
                (android.hardware.fingerprint.FingerprintManager) getSystemService(Context.FINGERPRINT_SERVICE);
        if (manager == null || !manager.isHardwareDetected() || !manager.hasEnrolledFingerprints()) {
            throw new IllegalStateException("Fingerprint authentication unavailable");
        }
        legacyBiometricDialog = new AlertDialog.Builder(this)
                .setTitle(getString(R.string.biometric_title))
                .setMessage(getString(action == BiometricAction.ENROLL
                        ? R.string.biometric_prompt_enroll : R.string.biometric_hint))
                .setNegativeButton(getString(R.string.biometric_cancel),
                        (dialog, which) -> { if (activeAuthSignal != null) activeAuthSignal.cancel(); })
                .setOnCancelListener(dialog -> { if (activeAuthSignal != null) activeAuthSignal.cancel(); })
                .show();
        manager.authenticate(new android.hardware.fingerprint.FingerprintManager.CryptoObject(cipher),
                activeAuthSignal, 0,
                new android.hardware.fingerprint.FingerprintManager.AuthenticationCallback() {
                    @Override
                    public void onAuthenticationSucceeded(
                            android.hardware.fingerprint.FingerprintManager.AuthenticationResult result) {
                        android.hardware.fingerprint.FingerprintManager.CryptoObject crypto = result.getCryptoObject();
                        onBiometricAuthenticated(generation, action,
                                crypto == null ? null : crypto.getCipher());
                    }

                    @Override
                    public void onAuthenticationError(int code, CharSequence message) {
                        onBiometricError(generation, action, message);
                    }

                    @Override
                    public void onAuthenticationFailed() {
                        runOnUiThread(() -> {
                            if (generation == authGeneration && txtLockHint != null)
                                txtLockHint.setText(R.string.biometric_fail);
                        });
                    }
                }, new Handler(Looper.getMainLooper()));
    }

    private void onBiometricAuthenticated(int generation, BiometricAction action, Cipher cipher) {
        runOnUiThread(() -> {
            if (generation != authGeneration || isFinishing()) return;
            unlockPending = false;
            activeAuthSignal = null;
            dismissLegacyBiometricDialog();
            if (cipher == null) {
                onBiometricError(generation, action, getString(R.string.biometric_fail));
                return;
            }
            if (action == BiometricAction.ENROLL) {
                boolean saved = false;
                try {
                    saved = biometricTokenStore.encryptAndEnable(
                            pendingEnrollmentExpiry, cipher);
                } catch (Exception ignored) { }
                pendingEnrollmentExpiry = 0L;
                if (saved) {
                    lockEnabled = true;
                    unlockedOnce = true;
                }
                notifyProfileBiometricResult("enable", saved);
                return;
            }

            try {
                if (!biometricTokenStore.hasUsableToken()) throw new IllegalStateException("Token expired");
                String storedValue = biometricTokenStore.decrypt(cipher);
                if (BiometricTokenStore.isSessionMarker(storedValue)) {
                    beginSessionRestore();
                } else {
                    throw new IllegalArgumentException("Invalid biometric session marker");
                }
            } catch (Exception e) {
                biometricTokenStore.clearTokenKeepEnabled();
                txtLockHint.setText(R.string.biometric_token_unavailable);
            }
        });
    }

    private void onBiometricError(int generation, BiometricAction action, CharSequence message) {
        runOnUiThread(() -> {
            if (generation != authGeneration) return;
            unlockPending = false;
            activeAuthSignal = null;
            dismissLegacyBiometricDialog();
            pendingEnrollmentExpiry = 0L;
            if (action == BiometricAction.ENROLL) {
                notifyProfileBiometricResult("enable", false);
            } else if (txtLockHint != null) {
                if (message == null || message.length() == 0) txtLockHint.setText(R.string.biometric_fail);
                else txtLockHint.setText(message);
            }
        });
    }

    private void beginSessionRestore() {
        if (!isOurUrl(startUrl) || webView == null) {
            failSessionRestore(false);
            return;
        }
        restorationPending = true;
        verifyingRestoredSession = false;
        biometricSessionLanding = null;
        txtLockHint.setText(R.string.biometric_ok);
        mainHandler.removeCallbacks(restorationTimeout);
        mainHandler.postDelayed(restorationTimeout, SESSION_RESTORE_TIMEOUT_MS);
        webView.loadUrl("https://" + expectedHost + "/api/v1/auth/biometric-session");
    }

    private void onSessionRestorePageCommitted(String url) {
        if (!isOurUrl(url) || isLoginUrl(url) || isBiometricSessionUrl(url)) {
            failSessionRestore(true);
            return;
        }
        if (isProfileConfirmationUrl(url)) {
            if (!verifyingRestoredSession) biometricSessionLanding = url;
            completeSessionRestore();
            return;
        }
        if (!verifyingRestoredSession) {
            verifyingRestoredSession = true;
            biometricSessionLanding = url;
            webView.loadUrl("https://" + expectedHost + "/Profile/Index");
            return;
        }
        failSessionRestore(true);
    }

    private void completeSessionRestore() {
        restorationPending = false;
        verifyingRestoredSession = false;
        mainHandler.removeCallbacks(restorationTimeout);
        android.webkit.CookieManager.getInstance().flush();
        unlockedOnce = true;
        String destination = pendingDestination;
        pendingDestination = null;
        if (destination == null) destination = biometricSessionLanding;
        biometricSessionLanding = null;
        if (destination != null && isOurUrl(destination) && !destination.equals(currentTopLevelUrl)) {
            webView.loadUrl(destination);
        }
        mainHandler.postDelayed(() -> applyLockGate(false), 180);
    }

    private void failSessionRestore(boolean discardToken) {
        if (!restorationPending && !lockEnabled) return;
        restorationPending = false;
        verifyingRestoredSession = false;
        biometricSessionLanding = null;
        mainHandler.removeCallbacks(restorationTimeout);
        if (discardToken && biometricTokenStore != null) biometricTokenStore.clearTokenKeepEnabled();
        if (txtLockHint != null) txtLockHint.setText(R.string.biometric_session_failed);
    }

    private void beginAccountLogin() {
        cancelPendingAuthentication();
        restorationPending = false;
        verifyingRestoredSession = false;
        mainHandler.removeCallbacks(restorationTimeout);
        if (biometricTokenStore == null || webView == null) return;
        try { biometricTokenStore.disable(); } catch (Exception ignored) { }
        lockEnabled = false;
        accountLoginPending = true;
        lockGate.setVisibility(View.GONE);
        hideOffline();
        webView.setVisibility(View.VISIBLE);
        webView.loadUrl("https://" + expectedHost + "/Account/Login?returnUrl=%2FProfile%2FIndex");
    }

    private void finishAccountLogin() {
        accountLoginPending = false;
        unlockedOnce = true;
        String destination = pendingDestination;
        pendingDestination = null;
        applyLockGate(false);
        if (destination != null && isOurUrl(destination)) webView.loadUrl(destination);
    }

    private void cancelPendingAuthentication() {
        authGeneration++;
        if (activeAuthSignal != null) {
            try { activeAuthSignal.cancel(); } catch (Exception ignored) { }
        }
        activeAuthSignal = null;
        dismissLegacyBiometricDialog();
        unlockPending = false;
        pendingEnrollmentExpiry = 0L;
    }

    private void dismissLegacyBiometricDialog() {
        if (legacyBiometricDialog != null) {
            legacyBiometricDialog.dismiss();
            legacyBiometricDialog = null;
        }
    }

    private void notifyProfileBiometricResult(String action, boolean success) {
        if (webView == null || !isProfileSettingsPage()) return;
        String js = "window.SugarShopBiometric&&window.SugarShopBiometric.onResult('"
                + action + "'," + (success ? "true" : "false") + ");";
        webView.evaluateJavascript(js, null);
    }

    boolean isBiometricLockEnabled() {
        return biometricTokenStore != null && biometricTokenStore.isEnabled();
    }

    boolean isBridgePageTrusted() {
        return isOurUrl(currentTopLevelUrl);
    }

    boolean isOtpPageTrusted() {
        return isBridgePageTrusted() && isLoginUrl(currentTopLevelUrl);
    }

    void startOtpListener() {
        if (!isOtpPageTrusted() || smsOtpReceiver == null) return;
        if (checkSelfPermission(android.Manifest.permission.RECEIVE_SMS)
                == android.content.pm.PackageManager.PERMISSION_GRANTED) {
            smsOtpReceiver.start(this);
        } else {
            requestPermissions(new String[] { android.Manifest.permission.RECEIVE_SMS }, SMS_PERMISSION_REQUEST);
        }
    }

    void stopOtpListener() {
        if (smsOtpReceiver != null) smsOtpReceiver.stop(this);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == SMS_PERMISSION_REQUEST && grantResults.length > 0
                && grantResults[0] == android.content.pm.PackageManager.PERMISSION_GRANTED
                && isOtpPageTrusted() && smsOtpReceiver != null) {
            smsOtpReceiver.start(this);
        }
    }

    private boolean isProfileSettingsPage() {
        if (!isBridgePageTrusted()) return false;
        String path = Uri.parse(currentTopLevelUrl).getPath();
        return path != null && ("/Profile".equalsIgnoreCase(path)
                || "/Profile/Index".equalsIgnoreCase(path));
    }

    void disableBiometricLockFromProfile() {
        runOnUiThread(() -> {
            if (!isProfileSettingsPage() || biometricTokenStore == null) {
                notifyProfileBiometricResult("disable", false);
                return;
            }
            boolean success = false;
            try { success = biometricTokenStore.disable(); } catch (Exception ignored) { }
            if (success) {
                lockEnabled = false;
                unlockedOnce = true;
                applyLockGate(false);
            }
            notifyProfileBiometricResult("disable", success);
        });
    }

    private void attachBridge() {
        if (webView == null || appBridge == null || bridgeAttached) return;
        webView.addJavascriptInterface(appBridge, "SugarShopApp");
        bridgeAttached = true;
    }

    private void detachBridge() {
        if (webView == null || !bridgeAttached) return;
        webView.removeJavascriptInterface("SugarShopApp");
        bridgeAttached = false;
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
        if (isOtpPageTrusted() && smsOtpReceiver != null
                && checkSelfPermission(android.Manifest.permission.RECEIVE_SMS)
                == android.content.pm.PackageManager.PERMISSION_GRANTED) {
            smsOtpReceiver.start(this);
        }
    }

    @Override
    protected void onPause() {
        if (smsOtpReceiver != null) smsOtpReceiver.stop(this);
        super.onPause();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        if (intent != null && webView != null) {
            String url = getIncomingUrl(intent);
            if (url != null) {
                if (lockEnabled && ((lockGate != null && lockGate.getVisibility() == View.VISIBLE)
                        || accountLoginPending || restorationPending)) {
                    pendingDestination = url;
                } else {
                    runOnUiThread(() -> webView.loadUrl(url));
                }
            }
        }
    }

    private boolean isOurUrl(String url) {
        try {
            return url != null && isFirstPartyUri(Uri.parse(url));
        } catch (Exception e) {
            return false;
        }
    }

    private boolean isFirstPartyUri(Uri uri) {
        return expectedHost != null && uri != null && !uri.isOpaque()
                && "https".equalsIgnoreCase(uri.getScheme())
                && uri.getHost() != null && expectedHost.equalsIgnoreCase(uri.getHost())
                && uri.getUserInfo() == null && (uri.getPort() == -1 || uri.getPort() == 443);
    }

    private static boolean isSecureHttpUri(Uri uri) {
        return uri != null && !uri.isOpaque() && "https".equalsIgnoreCase(uri.getScheme())
                && uri.getHost() != null && uri.getUserInfo() == null
                && (uri.getPort() == -1 || uri.getPort() == 443);
    }

    private static boolean isPaymentGatewayUri(Uri uri) {
        if (!isSecureHttpUri(uri)) return false;
        String host = uri.getHost().toLowerCase(Locale.US);
        return host.equals("zibal.ir") || host.endsWith(".zibal.ir");
    }

    private boolean isPaymentGatewayPage() {
        try { return currentTopLevelUrl != null && isPaymentGatewayUri(Uri.parse(currentTopLevelUrl)); }
        catch (Exception ignored) { return false; }
    }

    private boolean routeNavigation(String rawUrl) {
        Uri uri;
        try { uri = Uri.parse(rawUrl); } catch (Exception ignored) { return true; }
        if (restorationPending && !isFirstPartyUri(uri)) failSessionRestore(true);
        if (accountLoginPending) {
            if (isFirstPartyUri(uri) && isAccountLoginFlowUrl(uri)) return false;
            return true;
        }
        if (isFirstPartyUri(uri) || isPaymentGatewayUri(uri)) return false;
        if (!isSafeExternalUri(uri)) return true;
        openExternal(uri);
        return true;
    }

    private void openExternal(Uri uri) {
        try { startActivity(new Intent(Intent.ACTION_VIEW, uri)); }
        catch (ActivityNotFoundException ignored) { }
    }

    private boolean isSafeExternalUri(Uri uri) {
        if (uri == null || uri.isOpaque() || uri.getHost() == null || uri.getUserInfo() != null) return false;
        String scheme = uri.getScheme();
        return "https".equalsIgnoreCase(scheme) || "http".equalsIgnoreCase(scheme);
    }

    private boolean isHtmlDocumentRequest(android.webkit.WebResourceRequest request) {
        String accept = request.getRequestHeaders().get("Accept");
        return accept != null && accept.toLowerCase(Locale.US).contains("text/html");
    }

    private String getIncomingUrl(Intent intent) {
        if (intent == null) return null;
        Uri data = intent.getData();
        if (data != null && isFirstPartyUri(data)) return data.toString();
        String extra = intent.getStringExtra("url");
        return isOurUrl(extra) ? extra : null;
    }

    private boolean isLoginUrl(String url) {
        if (!isOurUrl(url)) return false;
        String path = Uri.parse(url).getPath();
        return path != null && ("/Account/Login".equalsIgnoreCase(path)
                || "/Identity/Account/Login".equalsIgnoreCase(path));
    }

    private boolean isBiometricSessionUrl(String url) {
        if (!isOurUrl(url)) return false;
        return "/api/v1/auth/biometric-session".equalsIgnoreCase(Uri.parse(url).getPath());
    }

    private boolean isAuthenticatedProfileUrl(String url) {
        if (!isOurUrl(url)) return false;
        String path = Uri.parse(url).getPath();
        return path != null && ("/Profile".equalsIgnoreCase(path)
                || "/Profile/Index".equalsIgnoreCase(path)
                || isPathOrChild(path, "/Admin")
                || isPathOrChild(path, "/Chef"));
    }

    private boolean isProfileConfirmationUrl(String url) {
        if (!isOurUrl(url)) return false;
        String path = Uri.parse(url).getPath();
        return path != null && ("/Profile".equalsIgnoreCase(path)
                || "/Profile/Index".equalsIgnoreCase(path));
    }

    private boolean isAccountLoginFlowUrl(Uri uri) {
        String path = uri.getPath();
        if (path == null) return false;
        return "/Account/Login".equalsIgnoreCase(path)
                || "/Profile".equalsIgnoreCase(path)
                || "/Profile/Index".equalsIgnoreCase(path)
                || isPathOrChild(path, "/Admin")
                || isPathOrChild(path, "/Chef");
    }

    private static boolean isPathOrChild(String path, String root) {
        return root.equalsIgnoreCase(path)
                || (path.length() > root.length()
                && path.regionMatches(true, 0, root + "/", 0, root.length() + 1));
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
        mainHandler.removeCallbacks(restorationTimeout);
        cancelPendingAuthentication();
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
            detachBridge();
            webView.destroy();
        }
        super.onDestroy();
    }
}
