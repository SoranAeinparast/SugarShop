package ir.soransoftpro.pastry;

import android.content.Context;
import android.content.BroadcastReceiver;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.Build;
import android.telephony.SmsMessage;
import android.util.Log;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * گوش‌دهنده‌ی پیامک‌های ورودی برای استخراج خودکار کد OTP شش‌رقمی.
 * الگوی جستجو: «کد ۱۲۳۴۵۶» یا «کد ورود: 123456» یا هر عدد ۶ رقمی مستقل در متن پیام
 * حاوی «کد» (فارسی/انگلیسی). فقط در حین صفحه‌ی ورود فعال است (start/stop از پل JS)
 * تا حریم خصوصی حفظ شود — هیچ پیامکی خارج از فرآیند ورود خوانده نمی‌شود.
 * 
 * نکته: در Android 14+ پیامک‌های واردشده با CREATE_REPLACE ممکن است به_receiver
 * معمولی نرسند؛ در آن حالت کاربر کد را دستی وارد می‌کند (رفتار fallback طبیعی).
 */
public class SmsOtpReceiver {

    /** الگوی «کد ... ۶ رقم» — هم اعداد لاتین و هم ارقام فارسی را می‌گیرد */
    private static final Pattern OTP_FA = Pattern.compile("(?:کد|code)\\s*[:：]?\\s*([0-9\u06F0-\u06F9]{6})");
    /** هر عدد ۶ رقمی مستقل در پیام حاوی کلمه‌ی کد */
    private static final Pattern OTP_ANY6 = Pattern.compile("(?<![0-9\u06F0-\u06F9])([0-9\u06F0-\u06F9]{6})(?![0-9\u06F0-\u06F9])");

    private BroadcastReceiver receiver;
    private volatile String lastCode = null;

    public SmsOtpReceiver() { }

    /** شروع گوش‌دادن — فقط وقتی صفحه‌ی ورود باز است از پل JS صدا زده می‌شود */
    public synchronized void start(Context context) {
        if (receiver != null) return; // همین حالا فعال است
        lastCode = null;
        receiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context ctx, Intent intent) {
                try {
                    if (!"android.provider.Telephony.SMS_RECEIVED".equals(intent.getAction())) return;
                    Object[] pdus = (Object[]) intent.getExtras().get("pdus");
                    if (pdus == null) return;
                    StringBuilder body = new StringBuilder();
                    for (Object pdu : pdus) {
                        SmsMessage msg;
                        if (Build.VERSION.SDK_INT >= 23) {
                            msg = SmsMessage.createFromPdu((byte[]) pdu, intent.getStringExtra("format"));
                        } else {
                            msg = SmsMessage.createFromPdu((byte[]) pdu);
                        }
                        if (msg != null && msg.getMessageBody() != null) {
                            body.append(msg.getMessageBody()).append(' ');
                        }
                    }
                    String code = extractOtp(body.toString());
                    if (code != null) lastCode = code;
                } catch (Throwable t) {
                    Log.w("SugarShopOtp", "sms parse failed", t);
                }
            }
        };
        IntentFilter f = new IntentFilter("android.provider.Telephony.SMS_RECEIVED");
        f.setPriority(IntentFilter.SYSTEM_HIGH_PRIORITY - 1);
        if (Build.VERSION.SDK_INT >= 33) {
            context.registerReceiver(receiver, f, Context.RECEIVER_EXPORTED);
        } else {
            context.registerReceiver(receiver, f);
        }
    }

    /** توقف گوش‌دادن — بعد از خواندن کد یا خروج از صفحه‌ی ورود */
    public synchronized void stop(Context context) {
        if (receiver == null) return;
        try { context.unregisterReceiver(receiver); } catch (Exception ignored) { }
        receiver = null;
    }

    /** آخرین کد استخراج‌شده (فقط وقتی receiver فعال است مقدار دارد) */
    public synchronized String peek() {
        return lastCode;
    }

    /** گرفتن و پاک کردن کد — یک‌بار مصرف */
    public synchronized String take() {
        String c = lastCode;
        lastCode = null;
        return c;
    }

    /** استخراج کد ۶ رقمی از متن پیامک */
    static String extractOtp(String text) {
        if (text == null || text.isEmpty()) return null;
        // اول الگوی صریح «کد ...»
        Matcher m = OTP_FA.matcher(text);
        if (m.find()) return faToEn(m.group(1));
        // در پیام‌های حاوی «کد»، اولین عدد ۶ رقمی مستقل
        if (text.contains("کد") || text.toLowerCase().contains("code")) {
            m = OTP_ANY6.matcher(text);
            if (m.find()) return faToEn(m.group(1));
        }
        return null;
    }

    private static String faToEn(String s) {
        StringBuilder sb = new StringBuilder();
        for (char ch : s.toCharArray()) {
            if (ch >= '\u06F0' && ch <= '\u06F9') sb.append((char) ('0' + (ch - '\u06F0')));
            else sb.append(ch);
        }
        return sb.toString();
    }
}
