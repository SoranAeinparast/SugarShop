package ir.soransoftpro.pastry;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.webkit.JavascriptInterface;
import android.os.Handler;
import android.os.Looper;

/**
 * پل کوچک JS ↔ اپ: صفحه می‌تواند وضعیت اپ را ببیند (مثلاً برای مخفی کردن بنر نصب)،
 * قفل بیومتریک اپ را روشن/خاموش کند (از تنظیمات پروفایل کاربر) و کد OTP پیامک‌شده
 * را به‌صورت خودکار بگیرد (فقط وقتی صفحه‌ی ورود فعال باشد).
 */
public class BackHandler {
    private final Activity activity;
    private final SmsOtpReceiver otpReceiver;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    public BackHandler(Activity a) { this(a, null); }

    public BackHandler(Activity a, SmsOtpReceiver otpReceiver) {
        activity = a;
        this.otpReceiver = otpReceiver;
    }

    @JavascriptInterface
    public boolean isAndroidApp() {
        return true;
    }

    @JavascriptInterface
    public String appVersion() {
        return "1.7.1";
    }

    // ── قفل بیومتریک ──

    @JavascriptInterface
    public boolean isBiometricAvailable() {
        // بررسی چندلایه — برخی سازندگان (شیائومی، هواوی و...) از BiometricManager
        // به‌اشتباه «ثبت‌نشده» برمی‌گردانند حتی وقتی اثر انگشت ثبت شده است؛
        // لایه‌ی FingerprintManager حکم نهایی را می‌دهد.
        try {
            if (android.os.Build.VERSION.SDK_INT >= 30) {
                android.hardware.biometrics.BiometricManager bm =
                        (android.hardware.biometrics.BiometricManager) activity.getSystemService(Context.BIOMETRIC_SERVICE);
                if (bm != null && bm.canAuthenticate(
                        android.hardware.biometrics.BiometricManager.Authenticators.BIOMETRIC_WEAK)
                        == android.hardware.biometrics.BiometricManager.BIOMETRIC_SUCCESS) {
                    return true;
                }
            }
        } catch (Throwable ignored) { }
        try {
            if (android.os.Build.VERSION.SDK_INT >= 28) {
                android.hardware.biometrics.BiometricManager bm =
                        (android.hardware.biometrics.BiometricManager) activity.getSystemService(Context.BIOMETRIC_SERVICE);
                if (bm != null && bm.canAuthenticate()
                        == android.hardware.biometrics.BiometricManager.BIOMETRIC_SUCCESS) {
                    return true;
                }
            }
        } catch (Throwable ignored) { }
        // ── لایه‌ی تعیین‌کننده: خود سنسور اثر انگشت ──
        try {
            if (android.os.Build.VERSION.SDK_INT >= 23) {
                android.hardware.fingerprint.FingerprintManager fm =
                        (android.hardware.fingerprint.FingerprintManager) activity.getSystemService(Context.FINGERPRINT_SERVICE);
                if (fm != null && fm.isHardwareDetected() && fm.hasEnrolledFingerprints()) {
                    return true;
                }
            }
        } catch (Throwable ignored) { }
        // ── آخرین لایه: صفحه‌قفل امن (اثر انگورتا/چهره/PIN) ──
        try {
            android.app.KeyguardManager km =
                    (android.app.KeyguardManager) activity.getSystemService(Context.KEYGUARD_SERVICE);
            if (km != null && km.isKeyguardSecure()) return true;
        } catch (Throwable ignored) { }
        return false;
    }

    @JavascriptInterface
    public boolean isBiometricLockEnabled() {
        SharedPreferences p = activity.getSharedPreferences("app_lock", Activity.MODE_PRIVATE);
        return p.getBoolean("biometric_enabled", false);
    }

    /** سایت این را صدا می‌زند تا قفل اپ فعال شود (پس از تأیید کاربر در صفحه‌ی سایت) */
    @JavascriptInterface
    public void enableBiometricLock() {
        activity.getSharedPreferences("app_lock", Activity.MODE_PRIVATE)
                .edit().putBoolean("biometric_enabled", true).apply();
    }

    @JavascriptInterface
    public void disableBiometricLock() {
        activity.getSharedPreferences("app_lock", Activity.MODE_PRIVATE)
                .edit().putBoolean("biometric_enabled", false).apply();
    }

    // ── کد OTP خودکار از پیامک ──

    /** شروع گوش‌دادن به پیامک‌ها — فقط صفحه‌ی ورود صدا می‌زند */
    @JavascriptInterface
    public void startOtpListener() {
        if (otpReceiver == null) return;
        otpReceiver.start(activity);
    }

    /** پایان گوش‌دادن */
    @JavascriptInterface
    public void stopOtpListener() {
        if (otpReceiver == null) return;
        otpReceiver.stop(activity);
    }

    /**
     * آخرین کد ۶ رقمی پیامک‌شده (یا رشته‌ی خالی). از نظر JS غیرهمگام است:
     * صفحه‌ی ورود آن را هر ۵۰۰ms پول می‌کند تا کد برسد.
     */
    @JavascriptInterface
    public String pollOtpCode() {
        if (otpReceiver == null) return "";
        String c = otpReceiver.take();
        return c == null ? "" : c;
    }
}
