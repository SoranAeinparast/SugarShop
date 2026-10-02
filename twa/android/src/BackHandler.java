package ir.soransoftpro.pastry;

import android.app.Activity;
import android.content.Context;
import android.webkit.JavascriptInterface;

/**
 * پل کوچک JS ↔ اپ: صفحه می‌تواند وضعیت اپ را ببیند (مثلاً برای مخفی کردن بنر نصب)،
 * قفل بیومتریک اپ را روشن/خاموش کند (از تنظیمات پروفایل کاربر) و کد OTP پیامک‌شده
 * را به‌صورت خودکار بگیرد (فقط وقتی صفحه‌ی ورود فعال باشد).
 */
public class BackHandler {
    private final Activity activity;
    private final SmsOtpReceiver otpReceiver;

    public BackHandler(Activity a) { this(a, null); }

    public BackHandler(Activity a, SmsOtpReceiver otpReceiver) {
        activity = a;
        this.otpReceiver = otpReceiver;
    }

    @JavascriptInterface
    public boolean isAndroidApp() {
        return isTrustedPage();
    }

    @JavascriptInterface
    public String appVersion() {
        return "1.7.1";
    }

    // ── قفل بیومتریک ──

    @JavascriptInterface
    public boolean isBiometricAvailable() {
        if (!isTrustedPage()) return false;
        try {
            if (android.os.Build.VERSION.SDK_INT >= 28) {
                android.hardware.biometrics.BiometricManager bm =
                        (android.hardware.biometrics.BiometricManager) activity.getSystemService(Context.BIOMETRIC_SERVICE);
                if (bm != null) {
                    int result = android.os.Build.VERSION.SDK_INT >= 30
                            ? bm.canAuthenticate(android.hardware.biometrics.BiometricManager.Authenticators.BIOMETRIC_STRONG)
                            : bm.canAuthenticate();
                    if (result == android.hardware.biometrics.BiometricManager.BIOMETRIC_SUCCESS) return true;
                }
            }
        } catch (Throwable ignored) { }
        try {
            if (android.os.Build.VERSION.SDK_INT >= 23) {
                android.hardware.fingerprint.FingerprintManager fm =
                        (android.hardware.fingerprint.FingerprintManager) activity.getSystemService(Context.FINGERPRINT_SERVICE);
                if (fm != null && fm.isHardwareDetected() && fm.hasEnrolledFingerprints()) {
                    return true;
                }
            }
        } catch (Throwable ignored) { }
        return false;
    }

    @JavascriptInterface
    public boolean isBiometricLockEnabled() {
        return isTrustedPage() && ((MainActivity) activity).isBiometricLockEnabled();
    }

    /** Called after the authenticated profile page receives a cookie-only biometric session. */
    @JavascriptInterface
    public void enableBiometricLock(String expiresAtMillis) {
        if (!isTrustedPage()) return;
        long expiry = 0L;
        try {
            expiry = Long.parseLong(expiresAtMillis);
        } catch (Exception ignored) { }
        ((MainActivity) activity).beginBiometricEnrollment(expiry);
    }

    @JavascriptInterface
    public void disableBiometricLock() {
        if (!isTrustedPage()) return;
        ((MainActivity) activity).disableBiometricLockFromProfile();
    }

    private boolean isTrustedPage() {
        return activity instanceof MainActivity && ((MainActivity) activity).isBridgePageTrusted();
    }

    // ── کد OTP خودکار از پیامک ──

    /** شروع گوش‌دادن به پیامک‌ها — فقط صفحه‌ی ورود صدا می‌زند */
    @JavascriptInterface
    public void startOtpListener() {
        if (otpReceiver == null || !isOtpPageTrusted()) return;
        if (activity instanceof MainActivity) ((MainActivity) activity).runOnUiThread(
                () -> ((MainActivity) activity).startOtpListener());
    }

    /** پایان گوش‌دادن */
    @JavascriptInterface
    public void stopOtpListener() {
        if (otpReceiver == null || !(activity instanceof MainActivity)) return;
        ((MainActivity) activity).runOnUiThread(() -> ((MainActivity) activity).stopOtpListener());
    }

    /**
     * آخرین کد ۶ رقمی پیامک‌شده (یا رشته‌ی خالی). از نظر JS غیرهمگام است:
     * صفحه‌ی ورود آن را هر ۵۰۰ms پول می‌کند تا کد برسد.
     */
    @JavascriptInterface
    public String pollOtpCode() {
        if (otpReceiver == null || !isOtpPageTrusted()) return "";
        String c = otpReceiver.take();
        return c == null ? "" : c;
    }

    private boolean isOtpPageTrusted() {
        return activity instanceof MainActivity && ((MainActivity) activity).isOtpPageTrusted();
    }
}
