using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using SugarShop.Web.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Domain.Entities.Sms;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Services.Sms
{
    /// <summary>
    /// سرویس اصلی سامانه پیامکی: قالب‌ها، ساعات مجاز، محدودیت‌های هزینه، OTP،
    /// اطلاع‌رسانی سفارش‌ها به کارکنان، یادآوری تولد، بازگشت مشتری و موجودی.
    /// </summary>
    public class SmsService
    {
        private readonly SugarShopSalesDbContext _db;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly SmsIrClient _smsIr;
        private readonly ISmsQueue _queue;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SmsService> _logger;
        private readonly IDataProtector _otpProtector;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly StatementLinkService _statementLinks;
        private readonly SmsLinkTrackingService _linkTracking;

        /// <summary>متغیرهای جای‌گذاری‌نشده‌ی قالب (مثل {OrderLink} وقتی آدرس سایت مشخص نیست).</summary>
        private static readonly System.Text.RegularExpressions.Regex LeftoverPlaceholder =
            new(@"\{[A-Za-z][A-Za-z0-9_]*\}", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// خطی که فقط یک متغیر است (مثل خط لینک در پیامک آماده‌پرداخت):
        /// اگر آن متغیر مقدار داشته باشد، همان خط دست‌نخورده می‌ماند و اگر نداشته باشد کل خط حذف می‌شود.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex LineWithOnlyPlaceholder =
            new(@"(?m)^[ \t]*\{(?<v>[A-Za-z][A-Za-z0-9_]*)\}[ \t]*\r?\n?", System.Text.RegularExpressions.RegexOptions.Compiled);

        public SmsService(
            SugarShopSalesDbContext db,
            SugarShopCatalogDbContext catalogDb,
            SmsIrClient smsIr,
            ISmsQueue queue,
            UserManager<ApplicationUser> userManager,
            ILogger<SmsService> logger,
            IDataProtectionProvider dataProtectionProvider,
            StatementLinkService statementLinks,
            SmsLinkTrackingService linkTracking,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _statementLinks = statementLinks;
            _linkTracking = linkTracking;
            _httpContextAccessor = httpContextAccessor;
            _db = db;
            _catalogDb = catalogDb;
            _smsIr = smsIr;
            _queue = queue;
            _userManager = userManager;
            _logger = logger;
            _otpProtector = dataProtectionProvider.CreateProtector("SugarShop.SmsOtp");
        }

        /// <summary>
        /// رمزنگاری کد یکبارمصرف پیش از ذخیره در دیتابیس؛ در صورت افشای دیتابیس،
        /// کدهای فعال قابل خواندن مستقیم نیستند.
        /// </summary>
        private string ProtectOtpCode(string code) => _otpProtector.Protect(code);

        /// <summary>
        /// مقایسه کد ذخیره‌شده با کد ارسالی به‌صورت زمان‌ثابت.
        /// ردیف‌هایی که پیش از رمزنگاری شدن کدها ساخته شده‌اند (متن ساده) هم پشتیبانی می‌شوند.
        /// </summary>
        private bool IsOtpCodeMatch(string? stored, string candidate)
        {
            if (string.IsNullOrEmpty(stored)) return false;

            string expected;
            try
            {
                expected = _otpProtector.Unprotect(stored);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                expected = stored;
            }
            catch (FormatException)
            {
                expected = stored;
            }

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(candidate));
        }

        // ═══════════════ تنظیمات و قالب‌ها ═══════════════

        public async Task<SmsSystemSetting> GetSettingsAsync()
        {
            var s = await _db.SmsSystemSettings.FirstOrDefaultAsync();
            if (s != null) return s;

            s = new SmsSystemSetting();
            _db.SmsSystemSettings.Add(s);
            await _db.SaveChangesAsync();
            return s;
        }

        public async Task<SmsTemplate?> GetTemplateAsync(SmsScenario scenario)
            => await _db.SmsTemplates.FirstOrDefaultAsync(t => t.Scenario == scenario);

        public static DateTime IranNow => IranClock.Now;

        private string? _siteName;

        /// <summary>نام واقعی سایت از تنظیمات سایت (به‌جای مقدار ثابت «شیرینی سرا»).</summary>
        private async Task<string> SiteNameAsync()
        {
            if (_siteName != null) return _siteName;
            var s = await _db.SiteSettings.FirstOrDefaultAsync();
            _siteName = string.IsNullOrWhiteSpace(s?.SiteTitle) ? "شیرینی سرا" : s.SiteTitle.Trim();
            return _siteName;
        }

        /// <summary>آیا الان در بازه سکوت شبانه است؟ (پیامک‌های تبلیغاتی ارسال نمی‌شوند)</summary>
        public static bool IsQuietHour(SmsSystemSetting s)
        {
            if (!s.RespectQuietHours) return false;
            var h = IranNow.Hour;
            return s.QuietStartHour <= s.QuietEndHour
                ? (h >= s.QuietStartHour && h < s.QuietEndHour)
                : (h >= s.QuietStartHour || h < s.QuietEndHour); // بازه شبانه‌روزی از ۲۲ تا ۸
        }

        public static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "";
            // ابتدا ارقام فارسی/عربی به انگلیسی تبدیل می‌شوند تا ورود کاربر با کیبورد فارسی هم درست کار کند
            var sb = new StringBuilder(phone.Length);
            foreach (var c in phone)
            {
                if (c >= '\u06F0' && c <= '\u06F9') sb.Append((char)('0' + (c - '\u06F0')));      // ۰-۹
                else if (c >= '\u0660' && c <= '\u0669') sb.Append((char)('0' + (c - '\u0660')));  // ٠-٩
                else sb.Append(c);
            }
            var digits = new string(sb.ToString().Where(char.IsDigit).ToArray());
            if (digits.StartsWith("0098")) digits = "0" + digits.Substring(4);
            if (digits.StartsWith("98") && digits.Length >= 12) digits = "0" + digits.Substring(2);
            if (digits.Length == 10 && digits.StartsWith("9")) digits = "0" + digits;
            return digits;
        }

        public static bool IsValidIranMobile(string phone)
            => NormalizePhone(phone).Length == 11 && NormalizePhone(phone).StartsWith("09");

        /// <summary>جایگذاری متغیرها در متن قالب: {CustomerName} {OrderCode} {SiteName} ...</summary>
        public static string Render(string template, Dictionary<string, string>? vars)
        {
            if (string.IsNullOrEmpty(template)) return "";

            // خط‌هایی که تنها یک متغیر دارند و مقدارش نیست، کامل حذف می‌شوند
            // (وگرنه یک خط خالی یا برچسب بی‌جواب مثل «پرداخت:» برای مشتری می‌ماند).
            template = LineWithOnlyPlaceholder.Replace(template, m =>
                vars != null && vars.ContainsKey(m.Groups["v"].Value) ? m.Value : "");

            if (vars != null && vars.Count > 0)
            {
                foreach (var kv in vars)
                    template = template.Replace("{" + kv.Key + "}", kv.Value);
            }

            // متغیری که مقدارش فراهم نشده (مثل {OrderLink} وقتی آدرس عمومی سایت مشخص نیست) نباید
            // به‌شکل «{OrderLink}» برای مشتری ارسال شود؛ جایش خالی می‌ماند.
            return LeftoverPlaceholder.Replace(template, "").TrimEnd();
        }

        private static string Fa(object? value)
        {
            var text = value?.ToString() ?? "";
            var map = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
            var sb = new StringBuilder();
            foreach (var c in text) sb.Append(c >= '0' && c <= '9' ? map[c - '0'] : c);
            return sb.ToString();
        }

        // ═══════════════ مسیر اصلی: ارسال سناریومحور ═══════════════

        /// <summary>
        /// ارسال پیامک برای یک سناریو: قالب فعال را می‌خواند، متغیرها را جایگذاری می‌کند،
        /// محدودیت‌ها (ساعات مجاز/سقف روزانه/بودجه) را چک می‌کند و در صف قرار می‌دهد.
        /// </summary>
        public async Task<bool> SendScenarioAsync(
            string? phone,
            SmsScenario scenario,
            Dictionary<string, string>? vars = null,
            SmsRecipientType recipientType = SmsRecipientType.Customer,
            bool force = false)
        {
            try
            {
                var settings = await GetSettingsAsync();
                if (!settings.IsEnabled) return false;

                var normalized = NormalizePhone(phone);
                if (normalized.Length != 11) return false;

                var template = await GetTemplateAsync(scenario);
                if (template == null || !template.IsActive) return false;

                // نام سایت همیشه از تنظیمات واقعی خوانده می‌شود و جایگزین {SiteName} می‌شود
                vars ??= new Dictionary<string, string>();
                vars["SiteName"] = await SiteNameAsync();
                var message = Render(template.BodyText, vars);

                // ساعات سکوت: پیامک‌های تراکنشی (غیر تبلیغاتی) همیشه مجازند
                bool promotional = scenario == SmsScenario.SpecialOffer || scenario == SmsScenario.WinBackDiscount
                    || scenario == SmsScenario.RestockAvailable || scenario == SmsScenario.BirthdayReminder;
                if (!force && promotional && IsQuietHour(settings))
                {
                    await WriteLogAsync(settings, normalized, message, SmsSendStatus.BlockedQuietHours, null, recipientType, scenario);
                    return false;
                }

                // سقف روزانه هر شماره (فقط تبلیغاتی) — ضد مزاحمت و هزینه
                if (promotional && settings.MaxSmsPerPhonePerDay > 0)
                {
                    // مرز «امروز» به وقت ایران محاسبه می‌شود تا سقف روزانه در نیمه‌شب ایران صفر شود
                    var midnightUtc = IranClock.DayStartUtc;
                    var todayCount = await _db.SmsLogs.CountAsync(l =>
                        l.PhoneNumber == normalized && l.SentAt >= midnightUtc &&
                        (l.Status == SmsSendStatus.Sent || l.Status == SmsSendStatus.Pending));
                    if (todayCount >= settings.MaxSmsPerPhonePerDay)
                    {
                        await WriteLogAsync(settings, normalized, message, SmsSendStatus.BlockedRateLimit, "سقف روزانه", recipientType, scenario);
                        return false;
                    }
                }

                // سقف بودجه ماهانه
                if (settings.MonthlyBudgetToman > 0)
                {
                    var monthStartUtc = IranClock.MonthStartUtc;
                    var monthCost = await _db.SmsLogs.Where(l => l.SentAt >= monthStartUtc).SumAsync(l => (decimal?)l.Cost) ?? 0;
                    if (monthCost >= settings.MonthlyBudgetToman)
                    {
                        await WriteLogAsync(settings, normalized, message, SmsSendStatus.BlockedDisabled, "اتمام بودجه ماهانه", recipientType, scenario);
                        return false;
                    }
                }

                await _queue.EnqueueAsync(new SmsQueueItem(normalized, message, scenario, recipientType));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendScenarioAsync failed: {Scenario}", scenario);
                return false;
            }
        }

        /// <summary>ارسال فوری (صف پردازنده این متد را صدا می‌زند). لاگ و خطا ذخیره می‌شود.</summary>
        public async Task SendNowAsync(string phone, string message, SmsScenario scenario,
            SmsRecipientType recipientType = SmsRecipientType.Customer, string? refKey = null)
        {
            var settings = await GetSettingsAsync();
            var normalized = NormalizePhone(phone);

            if (!settings.IsEnabled)
            {
                await WriteLogAsync(settings, normalized, message, SmsSendStatus.BlockedDisabled, "سامانه غیرفعال", recipientType, scenario);
                return;
            }

            SmsSendResult result;
            if (settings.SandboxMode || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                // حالت آزمایشی: هیچ پیامکی واقعاً ارسال نمی‌شود ولی لاگ کامل ثبت می‌گردد
                _logger.LogInformation("[SMS SANDBOX] to {Phone} ({Scenario}): {Message}", normalized, scenario, message);
                result = SmsSendResult.Ok(0, "sandbox");
            }
            else
            {
                result = await _smsIr.SendBulkAsync(settings.ApiKey, settings.SenderNumber, new[] { normalized }, message);
                if (!result.Success)
                {
                    // یک تلاش مجدد پس از ۳ ثانیه (خطای گذرا / قطعی موقت سرویس)
                    await Task.Delay(3000);
                    result = await _smsIr.SendBulkAsync(settings.ApiKey, settings.SenderNumber, new[] { normalized }, message);
                }
            }

            await WriteLogAsync(settings, normalized, message,
                result.Success ? SmsSendStatus.Sent : SmsSendStatus.Failed,
                result.ErrorMessage, recipientType, scenario, result.Cost, result.ProviderMessageId);
        }

        private async Task WriteLogAsync(SmsSystemSetting settings, string phone, string message,
            SmsSendStatus status, string? error, SmsRecipientType recipientType, SmsScenario scenario,
            decimal cost = 0, string? providerMessageId = null)
        {
            try
            {
                _db.SmsLogs.Add(new SmsLog
                {
                    PhoneNumber = phone,
                    MessageText = message,
                    ProviderUsed = SmsProviderType.SmsIr,
                    Status = status,
                    ErrorMessage = error,
                    Cost = cost,
                    SentAt = DateTime.UtcNow,
                    // شناسه پیامک در درگاه — برای استعلام وضعیت تحویل واقعی
                    ProviderMessageId = long.TryParse(providerMessageId, out var mid) ? mid : null,
                    DeliveryCheckedAt = null
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write SMS log for {Phone}", phone);
            }
        }

        // ═══════════════ وضعیت تحویل و گزارش زنده درگاه ═══════════════

        private static DateTime _lastDeliverySyncUtc = DateTime.MinValue;
        private static readonly TimeSpan DeliverySyncInterval = TimeSpan.FromMinutes(2);

        /// <summary>
        /// استعلام وضعیت تحویل پیامک‌های اخیر از sms.ir و به‌روزرسانی SmsLogs.
        /// کد ۶ یعنی «خطا در مخابرات» — اعتبار کسر شده ولی پیامک نرسیده؛ این موارد در گزارش مشخص می‌شوند.
        /// برای کند نشدن صفحات، حداکثر هر ۲ دقیقه یک‌بار و هر بار حداکثر ۲۰ پیامک استعلام می‌شود.
        /// </summary>
        public async Task<(int checkedCount, int updatedCount)> SyncDeliveryStatesAsync(bool force = false)
        {
            if (!force && (DateTime.UtcNow - _lastDeliverySyncUtc) < DeliverySyncInterval)
                return (0, 0);
            _lastDeliverySyncUtc = DateTime.UtcNow;

            var settings = await GetSettingsAsync();
            if (settings.SandboxMode || string.IsNullOrWhiteSpace(settings.ApiKey)) return (0, 0);

            // پیامک‌های موفق اخیرِ بدون وضعیت تحویل (حداکثر ۳ روز اخیر)
            var cutoff = DateTime.UtcNow.AddDays(-3);
            var pending = await _db.SmsLogs
                .Where(l => l.Status == SmsSendStatus.Sent
                    && l.ProviderMessageId != null
                    && l.DeliveryCheckedAt == null
                    && l.SentAt >= cutoff)
                .OrderByDescending(l => l.Id)
                .Take(20)
                .ToListAsync();
            if (pending.Count == 0) return (0, 0);

            int checkedCount = 0, updatedCount = 0;
            foreach (var log in pending)
            {
                var info = await _smsIr.GetDeliveryAsync(settings.ApiKey, log.ProviderMessageId!.Value);
                checkedCount++;
                if (info?.DeliveryState != null)
                {
                    log.DeliveryState = info.DeliveryState;
                    log.DeliveryCheckedAt = DateTime.UtcNow;
                    updatedCount++;
                }
            }
            await _db.SaveChangesAsync();
            return (checkedCount, updatedCount);
        }

        /// <summary>گزارش زنده ارسال‌های امروز مستقیماً از پنل sms.ir (مستقل از لاگ داخلی).</summary>
        public async Task<List<SmsDeliveryInfo>?> GetProviderLiveReportAsync()
        {
            var settings = await GetSettingsAsync();
            if (settings.SandboxMode || string.IsNullOrWhiteSpace(settings.ApiKey)) return null;
            return await _smsIr.GetLiveReportAsync(settings.ApiKey, 50);
        }

        /// <summary>اعتبار پنل بر حسب «تعداد پیامک» (واحد واقعی API اعتبار sms.ir).</summary>
        public async Task<decimal?> GetProviderCreditAsync()
        {
            var settings = await GetSettingsAsync();
            if (settings.SandboxMode || string.IsNullOrWhiteSpace(settings.ApiKey)) return null;
            return await _smsIr.GetCreditAsync(settings.ApiKey);
        }

        // ═══════════════ OTP (ورود و بازیابی رمز) ═══════════════

        /// <summary>
        /// تولید و ارسال کد یکبار مصرف. چندلایه ضد سوءاستفاده:
        /// سقف ساعتی هر شماره، سقف ساعتی هر IP، کش خنک ۶۰ ثانیه بین درخواست‌ها.
        /// </summary>
        public async Task<(bool ok, string message)> SendOtpAsync(string phone, string? ip, string purpose = "Login")
        {
            var settings = await GetSettingsAsync();
            if (!settings.IsEnabled) return (false, "سامانه پیامکی فعال نیست.");
            if (purpose == "Login" && !settings.OtpLoginEnabled) return (false, "ورود با کد یکبار مصرف فعال نیست.");
            if (purpose == "PasswordReset" && !settings.PasswordResetSmsEnabled) return (false, "بازیابی رمز با پیامک فعال نیست.");

            var normalized = NormalizePhone(phone);
            if (!IsValidIranMobile(normalized)) return (false, "شماره موبایل معتبر نیست. (مثال: 09123456789)");

            var nowUtc = DateTime.UtcNow;

            // کش خنک: بین هر دو درخواست حداقل ۶۰ ثانیه
            var last = await _db.SmsOtpCodes
                .Where(o => o.Phone == normalized && o.Purpose == purpose)
                .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();
            if (last != null && (nowUtc - last.CreatedAt).TotalSeconds < 60)
                return (false, "کد تازگی ارسال شده است؛ لطفاً یک دقیقه صبر کنید.");

            // سقف ساعتی هر شماره
            var hourAgo = nowUtc.AddHours(-1);
            var phoneCount = await _db.SmsOtpCodes.CountAsync(o => o.Phone == normalized && o.CreatedAt >= hourAgo);
            if (phoneCount >= Math.Max(1, settings.OtpPerPhonePerHour))
                return (false, "تعداد درخواست کد برای این شماره بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.");

            // سقف ساعتی هر IP
            if (!string.IsNullOrEmpty(ip) && settings.OtpPerIpPerHour > 0)
            {
                var ipCount = await _db.SmsOtpCodes.CountAsync(o => o.IpAddress == ip && o.CreatedAt >= hourAgo);
                if (ipCount >= settings.OtpPerIpPerHour)
                    return (false, "تعداد درخواست‌های شما بیش از حد مجاز است. لطفاً بعداً تلاش کنید.");
            }

            // ابطال کدهای قبلی همان شماره
            var olds = await _db.SmsOtpCodes.Where(o => o.Phone == normalized && o.Purpose == purpose && !o.IsUsed).ToListAsync();
            foreach (var o in olds) o.IsUsed = true;

            // کد با مولد تصادفی امن (غیرقابل پیش‌بینی) ساخته و محافظت‌شده ذخیره میشود
            var code = System.Security.Cryptography.RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString(CultureInfo.InvariantCulture);
            _db.SmsOtpCodes.Add(new SmsOtpCode
            {
                Phone = normalized,
                Code = ProtectOtpCode(code),
                Purpose = purpose,
                IpAddress = ip,
                CreatedAt = nowUtc,
                ExpiresAt = nowUtc.AddMinutes(5)
            });

            // ارسال: ابتدا تلاش با الگوی تأییدشده sms.ir (سریع‌تر و مطمئن‌تر)، در نبود الگو متن آزاد
            var template = await GetTemplateAsync(purpose == "Login" ? SmsScenario.Otp : SmsScenario.PasswordResetOtp);
            var message = Render(template?.BodyText ?? "کد ورود شما: {Code}\n{SiteName}",
                new Dictionary<string, string> { { "Code", Fa(code) }, { "SiteName", await SiteNameAsync() } });

            await _db.SaveChangesAsync(); // ثبت کد حتی قبل از ارسال (ارسال در همین درخواست — تراکنشی و ضروری)

            if (settings.SandboxMode || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                _logger.LogInformation("[SMS SANDBOX] OTP to {Phone}: {Code}", normalized, code);
                await WriteLogAsync(settings, normalized, message, SmsSendStatus.Sent, null, SmsRecipientType.Customer,
                    purpose == "Login" ? SmsScenario.Otp : SmsScenario.PasswordResetOtp);
                return (true, "کد تأیید ثبت شد (حالت آزمایشی — در پنل لاگ ببینید).");
            }

            var result = await _smsIr.SendBulkAsync(settings.ApiKey, settings.SenderNumber, new[] { normalized }, message);
            if (!result.Success)
            {
                _logger.LogError("OTP send failed for {Phone}: {Error}", normalized, result.ErrorMessage);
                await WriteLogAsync(settings, normalized, message, SmsSendStatus.Failed, result.ErrorMessage,
                    SmsRecipientType.Customer, purpose == "Login" ? SmsScenario.Otp : SmsScenario.PasswordResetOtp);
                return (false, "ارسال پیامک با خطا مواجه شد. لطفاً چند لحظه بعد دوباره تلاش کنید.");
            }
            await WriteLogAsync(settings, normalized, message, SmsSendStatus.Sent, null, SmsRecipientType.Customer,
                purpose == "Login" ? SmsScenario.Otp : SmsScenario.PasswordResetOtp, result.Cost);
            return (true, "کد تأیید به شماره شما پیامک شد.");
        }

        /// <summary>اعتبارسنجی کد OTP با محدودیت ۵ تلاش و انقضای ۵ دقیقه‌ای (پیامک گاهی دیر می‌رسد؛ ۲ دقیقه کوتاه بود).</summary>
        public async Task<bool> ValidateOtpAsync(string phone, string code, string purpose = "Login")
        {
            var normalized = NormalizePhone(phone);
            var otp = await _db.SmsOtpCodes
                .Where(o => o.Phone == normalized && o.Purpose == purpose && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();

            if (otp == null) return false;
            if (otp.ExpiresAt < DateTime.UtcNow) return false;
            if (otp.AttemptCount >= 5) return false;

            if (!IsOtpCodeMatch(otp.Code, (code ?? "").Trim()))
            {
                otp.AttemptCount++;
                await _db.SaveChangesAsync();
                return false;
            }

            otp.IsUsed = true;
            await _db.SaveChangesAsync();
            return true;
        }

        // ═══════════════ اطلاع‌رسانی سفارش‌ها ═══════════════

        /// <summary>
        /// مشخصات مشتریِ صاحب حساب (خریدار) از پروفایل کاربر خوانده می‌شود، نه گیرنده سفارش.
        /// مثال: محسن برای دوستش علی کیک می‌خرد → پیامک «محسن عزیز...» و در صورت نداشتن شماره، به شماره گیرنده می‌رود.
        /// </summary>
        private async Task<(string phone, Dictionary<string, string> vars)> OrderCustomerContextAsync(Order order)
        {
            ApplicationUser? user = null;
            if (!string.IsNullOrWhiteSpace(order.UserId))
                user = await _userManager.FindByIdAsync(order.UserId);

            var phone = NormalizePhone(user?.PhoneNumber);
            if (phone.Length != 11) phone = NormalizePhone(order.CustomerPhone);

            var buyerName = user?.FullName;
            if (string.IsNullOrWhiteSpace(buyerName) || LooksLikePlaceholder(buyerName))
                buyerName = order.CustomerName;

            var vars = OrderVars(order, buyerName, await OrderTotalForSmsAsync(order));
            return (phone, vars);
        }

        /// <summary>نام خودکار حساب‌های OTPِ بدون نام (مثل «کاربر0651») placeholder است.</summary>
        private static bool LooksLikePlaceholder(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            if (!name.StartsWith("کاربر")) return false;
            var rest = name["کاربر".Length..];
            return rest.Length <= 6 && rest.All(char.IsDigit);
        }

        /// <summary>ثبت سفارش جدید → مشتری (خریدار) + کارکنان انتخابی در تنظیمات.</summary>
        public async Task NotifyNewOrderAsync(Order order, bool needsWeighing)
        {
            var (phone, vars) = await OrderCustomerContextAsync(order);
            await SendScenarioAsync(phone, SmsScenario.OrderPlacedCustomer, vars, SmsRecipientType.Customer);

            var s = await GetSettingsAsync();
            if (!s.StaffAlertsEnabled) return;

            var roles = (s.NewOrderAlertRoles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (roles.Length == 0) roles = new[] { "Admin", "OrderManager", "Chef" }; // پیش‌فرض هوشمند
            foreach (var role in roles)
            {
                if (Enum.TryParse<SmsRecipientType>(role, out var rt))
                {
                    // سرآشپز: اگر تنظیم «فقط درخواست‌های کیک سفارشی برای سرآشپز مهم است» فعال باشد،
                    // فقط سفارش‌های نیازمند وزن‌کشی (جعبه شیرینی) به او اطلاع داده می‌شود
                    if (rt == SmsRecipientType.Chef && s.NewOrderAlertOnlyWeighing && !needsWeighing) continue;
                    await SendToRoleAsync(rt, SmsScenario.OrderPlacedStaff, vars);
                }
            }
            await SendToCustomPhonesAsync(s.NewOrderAlertPhones, SmsScenario.OrderPlacedStaff, vars);
        }

        /// <summary>
        /// وزن‌کشی تأیید شد → «سفارش شما آماده پرداخت است»، همراه با لینکی که مشتری را مستقیم
        /// روی صفحه «تأیید و پرداخت» مرحله دوم می‌نشاند (یک قدم تا درگاه). قبلاً این پیامک
        /// بدون لینک بود و مشتری باید خودش پنل را باز می‌کرد و سفارش را پیدا می‌کرد.
        /// متغیر {StatementLink} هم پر می‌شود تا اگر مدیر ترجیح داد ریز مبالغ را بدون ورود
        /// نشان دهد، در همان قالب قابل استفاده باشد.
        /// </summary>
        public async Task NotifyWeighingReadyAsync(Order order, string? baseUrlOverride = null)
        {
            var (phone, vars) = await OrderCustomerContextAsync(order);

            var baseUrl = await PublicBaseUrlAsync() ?? baseUrlOverride;
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                // ── لینک پیامک = آدرس اختصاصی و بدون‌ورود همان سفارش ──
                // /s/{token} کار می‌کند بدون این‌که مشتری وارد شود: ریز وزن و قیمت را می‌بیند و
                // از همان صفحه یک‌کلیکی به درگاه می‌رود (ورود از وسط مسیر حذف شده است).
                // نشانه pay=1 دو کار می‌کند: ۱) بازدید به نام همین پیامک («آماده پرداخت») در گزارش
                // اثرسنجی ثبت شود، ۲) پیام راهنمای صفحه بگوید لینک برای پرداخت آمده است.
                //
                // چرا نه آدرس کوتاه /p/{id}؟ چون آن آدرس قابل حدس است و اگر بدون توکن مبلغ را
                // نشان می‌داد یا توکن می‌ساخت، هر کسی با حدس زدن شماره سفارش صورت‌حساب بقیه را
                // می‌دید. آن آدرس فقط برای پیامک‌های قبلی باقی مانده است.
                var payLink = await StatementLinkAsync(order, baseUrl);
                if (payLink != null)
                    vars["OrderLink"] = $"{payLink}?pay=1";
                else
                    vars["OrderLink"] = $"{baseUrl.TrimEnd('/')}/p/{order.Id}"; // نبود توکن: مسیر قبلی (با ورود)

                var statementLink = await StatementLinkAsync(order, baseUrl);
                if (statementLink != null) vars["StatementLink"] = statementLink;

                // اثرسنجی: این سفارش یک پیامک لینک‌دار گرفت (مخرج «نرخ باز شدن» در گزارش ادمین)
                await _linkTracking.MarkSentAsync(order.Id, SmsLinkKind.WaitingPayment);
            }

            await SendScenarioAsync(phone, SmsScenario.WeighingReadyCustomer, vars);
        }

        /// <summary>
        /// پرداخت تأیید شد → مشتری. اگر لینک صورت‌حساب در دسترس باشد، متغیر {StatementLink}
        /// هم پر می‌شود تا مدیر بتواند در صورت تمایل هر دو اطلاع‌رسانی را در یک پیامک ادغام کند.
        /// </summary>
        public async Task NotifyPaymentConfirmedAsync(Order order, string? baseUrlOverride = null)
        {
            var (phone, vars) = await OrderCustomerContextAsync(order);

            var link = await StatementLinkAsync(order, baseUrlOverride);
            if (link != null) vars["StatementLink"] = link;

            await SendScenarioAsync(phone, SmsScenario.PaymentConfirmedCustomer, vars);
        }

        /// <summary>
        /// پرداخت موفق → لینک صورت‌حساب پرداخت برای مشتری.
        /// اگر آدرس عمومی سایت مشخص نباشد یا شماره‌ای نداشته باشیم، پیامکی ارسال نمی‌شود
        /// (پیامکِ بدون لینکِ کارآمد بی‌فایده است).
        /// </summary>
        public async Task NotifyPaymentStatementAsync(Order order, string? baseUrlOverride = null)
        {
            var (phone, vars) = await OrderCustomerContextAsync(order);
            if (phone.Length != 11) return;

            var link = await StatementLinkAsync(order, baseUrlOverride);
            if (link == null)
            {
                _logger.LogWarning("لینک صورت‌حساب سفارش {OrderId} ساخته نشد؛ آدرس عمومی سایت را در تنظیمات قالب (آدرس پایه‌ی اپلیکیشن) ثبت کنید.", order.Id);
                return;
            }

            vars["StatementLink"] = link;

            // اثرسنجی لینک صورت‌حساب (بازدیدهایش از /s/{token} ثبت می‌شود)
            await _linkTracking.MarkSentAsync(order.Id, SmsLinkKind.Statement);

            await SendScenarioAsync(phone, SmsScenario.PaymentStatementCustomer, vars);
        }

        /// <summary>
        /// آدرس عمومی سایت برای ساخت لینک داخل پیامک: اول از تنظیمات قالب («آدرس پایه‌ی اپلیکیشن»، همان مقداری
        /// که لینک دانلود اپ از آن ساخته می‌شود) و در نبودِ آن از دامنه‌ی همان درخواست جاری.
        /// در کارهای پس‌زمینه (بدون درخواست) اگر آدرس تنظیمات خالی باشد، null برمی‌گردد.
        /// </summary>
        private async Task<string?> PublicBaseUrlAsync()
        {
            var configured = (await _db.ThemeSettings.AsNoTracking().FirstOrDefaultAsync())?.AppBaseUrl;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var url = configured.Trim();
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
                return url.TrimEnd('/');
            }

            var request = _httpContextAccessor?.HttpContext?.Request;
            if (request == null || !request.Host.HasValue) return null;
            return $"{request.Scheme}://{request.Host}";
        }

        /// <summary>
        /// لینک اختصاصی و موقت صورت‌حساب پرداخت سفارش: آدرس کوتاه /s/{token} با توکنی که چند روز
        /// اعتبار دارد و بدون ورود باز می‌شود. کوتاه بودن آدرس مهم است چون هر کاراکتر اضافه در
        /// پیامک هزینه دارد. اگر ساخت توکن ناموفق باشد، پیامک فرستاده نمی‌شود
        /// (لینک بی‌اعتبار بدتر از نبودن لینک است).
        /// </summary>
        private async Task<string?> StatementLinkAsync(Order order, string? baseUrlOverride)
        {
            // آدرس تنظیم‌شده مقدم است چون همیشه عمومی و در دسترس است (حتی پشت پروکسی/آی‌آی‌اس)
            var baseUrl = await PublicBaseUrlAsync() ?? baseUrlOverride;
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            try
            {
                var token = await _statementLinks.GetOrCreateTokenAsync(order.Id, order.UserId);
                return token == null ? null : $"{baseUrl.TrimEnd('/')}/s/{token}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ساخت توکن صورت‌حساب برای سفارش {OrderId} ناموفق بود", order.Id);
                return null;
            }
        }

        /// <summary>تغییر وضعیت سفارش (آماده‌سازی/ارسال/تحویل) → مشتری.</summary>
        public async Task NotifyOrderStatusChangedAsync(Order order, string statusFa)
        {
            var (phone, vars) = await OrderCustomerContextAsync(order);
            vars["Status"] = statusFa;
            await SendScenarioAsync(phone, SmsScenario.OrderStatusChangedCustomer, vars);
        }

        public async Task NotifyCustomCakeStatusAsync(CustomCakeOrder cake, string statusFa)
        {
            var user = await _userManager.FindByIdAsync(cake.UserId);
            // نام و شماره صاحب حساب (خریدار) مقدم است؛ گیرنده فقط در نبودِ اطلاعات کاربر استفاده می‌شود
            var name = (!string.IsNullOrWhiteSpace(user?.FullName) && !LooksLikePlaceholder(user.FullName))
                ? user!.FullName
                : (cake.ReceiverName ?? user?.FullName);
            var phone = NormalizePhone(user?.PhoneNumber);
            if (phone.Length != 11) phone = cake.ReceiverPhone;
            var vars = new Dictionary<string, string>
            {
                { "CustomerName", ToNameParts(name) },
                { "CakeCode", Fa(cake.Id) },
                { "Status", statusFa }
            };
            await SendScenarioAsync(phone, SmsScenario.CustomCakeStatusCustomer, vars);
        }

        /// <summary>
        /// مبلغ واقعی پرداختی مشتری — دقیقاً همان منطق درگاه پرداخت (PaymentController):
        /// جمع محصولات قیمت‌ثابت + جمع جعبه‌های وزن‌کشی‌شده + هزینه پیک − تخفیف.
        /// (قبلاً هزینه پیک از پیامک جا می‌ماند و با مبلغ درگاه نمی‌خواند.)
        /// </summary>
        private async Task<decimal> OrderTotalForSmsAsync(Order order)
        {
            var productTotal = order.Items.Where(i => i.ItemType == OrderItemType.Product)
                .Sum(i => i.TotalPriceSnapshot);

            var boxTitles = order.Items
                .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                .Select(i => i.BoxTitle!).Distinct().ToList();

            var finalizedBoxTotal = await _db.BoxFinalInfos
                .Where(b => b.OrderId == order.Id && boxTitles.Contains(b.BoxTitle))
                .SumAsync(b => (decimal?)b.FinalPrice) ?? 0m;

            var total = productTotal + finalizedBoxTotal
                        + order.DeliveryFeeSnapshot
                        - (order.DiscountAmountSnapshot ?? 0);
            return total > 0 ? total : 0;
        }

        private static Dictionary<string, string> OrderVars(Order order, string buyerName, decimal amount)
        {
            return new Dictionary<string, string>
            {
                { "CustomerName", ToNameParts(buyerName) },
                { "OrderCode", Fa(order.OrderCode) },
                { "Amount", Fa(string.Create(CultureInfo.GetCultureInfo("fa-IR"), $"{amount:N0}")) },
                { "Status", "" }
            };
        }

        private static string ToNameParts(string? fullName)
        {
            var name = (fullName ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return "مشتری عزیز";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts[0];
        }

        /// <summary>پاک‌سازی پیشوند کد تخفیف: فقط حروف و اعداد انگلیسی بزرگ.</summary>
        private static string SanitizeCodePrefix(string? prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return "BACK";
            var clean = new string(prefix.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToUpperInvariant();
            return clean.Length >= 2 ? clean : "BACK";
        }

        // ═══════════════ کارکنان ═══════════════

        /// <summary>ارسال به همه کاربرانِ یک نقش که شماره موبایل ثبت‌شده دارند.</summary>
        public async Task SendToRoleAsync(SmsRecipientType role, SmsScenario scenario, Dictionary<string, string>? vars)
        {
            var users = await _userManager.GetUsersInRoleAsync(role.ToString());
            foreach (var u in users)
            {
                if (string.IsNullOrWhiteSpace(u.PhoneNumber)) continue;
                await SendScenarioAsync(u.PhoneNumber, scenario, vars, role);
            }
        }

        public async Task SendToCustomPhonesAsync(string? phonesCsv, SmsScenario scenario, Dictionary<string, string>? vars)
        {
            if (string.IsNullOrWhiteSpace(phonesCsv)) return;
            foreach (var p in phonesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                await SendScenarioAsync(p, scenario, vars, SmsRecipientType.CustomPhones);
        }

        /// <summary>سفارش کیک سفارشی جدید → مدیر / سرآشپز (طبق تنظیمات).</summary>
        public async Task NotifyNewCustomCakeAsync(CustomCakeOrder cake)
        {
            var s = await GetSettingsAsync();
            if (!s.CustomCakeAlertsEnabled) return;

            var user = await _userManager.FindByIdAsync(cake.UserId);
            var customerName = (!string.IsNullOrWhiteSpace(user?.FullName) && !LooksLikePlaceholder(user.FullName))
                ? user!.FullName
                : (cake.ReceiverName ?? "مشتری");
            var vars = new Dictionary<string, string>
            {
                { "CustomerName", ToNameParts(customerName) },
                { "OrderCode", Fa(cake.Id) },
                { "Amount", "—" }
            };
            await SendToCustomPhonesAsync(s.CustomCakeAlertPhones, SmsScenario.OrderPlacedStaff, vars);
        }

        // ═══════════════ تیکت‌ها ═══════════════

        public async Task NotifyTicketReceivedAsync(string userId, int ticketId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.PhoneNumber)) return;
            var vars = new Dictionary<string, string>
            {
                { "CustomerName", ToNameParts(user.FullName) },
                { "TicketId", Fa(ticketId) }
            };
            await SendScenarioAsync(user.PhoneNumber, SmsScenario.TicketReceivedCustomer, vars);
        }

        public async Task NotifyTicketAnsweredAsync(string userId, int ticketId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.PhoneNumber)) return;
            var vars = new Dictionary<string, string>
            {
                { "CustomerName", ToNameParts(user.FullName) },
                { "TicketId", Fa(ticketId) }
            };
            await SendScenarioAsync(user.PhoneNumber, SmsScenario.TicketAnsweredCustomer, vars);
        }

        // ═══════════════ موجودی ═══════════════

        /// <summary>وقتی محصول موجود شد → خبر دادن به همه مشترکینِ «خبرم کن».</summary>
        public async Task NotifyRestockAsync(int sweetItemId)
        {
            var s = await GetSettingsAsync();
            if (!s.RestockAlertsEnabled) return;

            var item = await _catalogDb.SweetItems.FindAsync(sweetItemId);
            if (item == null || !item.IsInStock) return;

            var subs = await _db.RestockSubscriptions
                .Where(r => r.SweetItemId == sweetItemId && !r.Notified)
                .ToListAsync();
            if (subs.Count == 0) return;

            var vars = new Dictionary<string, string> { { "ProductName", item.TitleFa } };
            foreach (var sub in subs)
            {
                await SendScenarioAsync(sub.Phone, SmsScenario.RestockAvailable, vars);
                sub.Notified = true;
                sub.NotifiedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }

        // ═══════════════ یادآوری پرداخت مرحله دوم (Hangfire ساعتی) ═══════════════

        /// <summary>
        /// سفارش‌هایی که پیامک «آماده پرداخت» گرفته‌اند ولی بعد از گذشت N ساعت هنوز پرداخت
        /// نشده‌اند، یک یادآوری کوتاه با همان لینک پرداخت می‌گیرند (هر سفارش فقط یک‌بار).
        ///
        /// لنگر زمان‌بندی، رکورد اثرسنجی لینک پرداخت (SmsLinkTracking با نوع WaitingPayment) است؛
        /// یعنی «لحظه‌ای که پیامک آماده‌پرداخت با لینک برای مشتری رفت» — نه زمان تأیید وزن‌کشی.
        /// دلیلش این است که یادآوری «با همان لینک پرداخت» فقط برای سفارشی معنا دارد که لینک
        /// پرداخت واقعاً برایش ارسال شده است.
        /// </summary>
        public async Task<int> SendPaymentRemindersAsync()
        {
            var s = await GetSettingsAsync();
            if (!s.IsEnabled || !s.PaymentReminderEnabled) return 0;

            var template = await GetTemplateAsync(SmsScenario.PaymentReminderCustomer);
            if (template == null || !template.IsActive) return 0;

            // یادآوری پرداخت پیام تبلیغاتی نیست، ولی بامداد فرستادنش هم فایده‌ای ندارد؛ در بازه
            // سکوت چیزی ارسال نمی‌شود و اولین اجرای بعد از پایان بازه آن را می‌فرستد.
            if (IsQuietHour(s)) return 0;

            // مقدار صفر یا منفی معنا ندارد؛ پیش‌فرض ۲۴ ساعت می‌شود (نه یک ساعت، که یعنی
            // «یادآوری تقریباً بلافاصله» و برای مشتری آزاردهنده است).
            var delayHours = s.PaymentReminderDelayHours > 0
                ? Math.Min(s.PaymentReminderDelayHours, 24 * 14)
                : 24;
            var cutoff = DateTime.UtcNow.AddHours(-delayHours);

            // شرط «منتظر پرداخت» عیناً همان حالتی است که تأیید وزن‌کشی می‌سازد:
            // IsPaymentEnabled = true و PaymentStatus = Unpaid و OrderStatus = PendingPayment
            var candidates = await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.IsPaymentEnabled
                    && o.PaymentStatus == PaymentStatus.Unpaid
                    && o.OrderStatus == OrderStatus.PendingPayment
                    && o.PaymentReminderSentAt == null
                    && _db.SmsLinkTrackings.Any(t => t.OrderId == o.Id
                        && t.Kind == SmsLinkKind.WaitingPayment
                        && t.SentAt != null && t.SentAt <= cutoff))
                .OrderBy(o => o.Id)
                .Take(50) // سقف هر اجرا: اگر سفارش‌های معلق زیادی جمع شده باشد یک‌جا پیامک نمی‌رود
                .ToListAsync();

            if (candidates.Count == 0) return 0;

            var baseUrl = await PublicBaseUrlAsync();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogWarning("یادآوری پرداخت ارسال نشد: «آدرس پایه‌ی اپلیکیشن» در تنظیمات قالب خالی است و بدون آن لینک پرداخت ساخته نمی‌شود.");
                return 0;
            }

            var siteName = await SiteNameAsync();
            var sent = 0;

            foreach (var order in candidates)
            {
                var (phone, vars) = await OrderCustomerContextAsync(order);
                if (phone.Length != 11) continue;

                // صفحه گوشی: همان ریز وزن و قیمت با مبلغ به‌روز، به‌همراه دکمه پرداخت.
                // اگر سفارش دیگر قابل پرداخت نباشد، خودِ آن صفحه حالت «پرداخت‌نشده/پرداخت‌شده» را نشان می‌دهد.
                vars["SiteName"] = siteName;

                var statementLink = await StatementLinkAsync(order, baseUrl);
                if (statementLink != null) vars["StatementLink"] = statementLink;

                // همان لینک اختصاصی بدون‌ورود، با نشانه pay=1 تا مشتری بدون ورود پرداخت کند
                vars["OrderLink"] = statementLink != null
                    ? $"{statementLink}?pay=1"
                    : $"{baseUrl.TrimEnd('/')}/p/{order.Id}";

                // سفارش تا وقتی پیامک در صف قرار نگرفته علامت‌گذاری نمی‌شود؛ پس اگر ارسال ناموفق
                // باشد (سامانه خاموش/قالب غیرفعال)، اجرای بعدی دوباره تلاش می‌کند و یادآوری گم نمی‌شود.
                if (!await SendScenarioAsync(phone, SmsScenario.PaymentReminderCustomer, vars))
                {
                    _logger.LogWarning("یادآوری پرداخت سفارش {OrderId} در صف قرار نگرفت (تنظیمات/قالب/ساعات سکوت).", order.Id);
                    continue;
                }

                order.PaymentReminderSentAt = DateTime.UtcNow;
                _db.SmsAutoReminderLogs.Add(new SmsAutoReminderLog
                {
                    ReminderType = SmsReminderType.PaymentReminder,
                    RefKey = $"PaymentReminder:{order.Id}",
                    Phone = phone,
                    Message = Render(template.BodyText, vars),
                    SentAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                sent++;
            }

            return sent;
        }

        // ═══════════════ یادآوری تولد (Hangfire روزانه) ═══════════════

        public async Task<int> SendBirthdayRemindersAsync()
        {
            var s = await GetSettingsAsync();
            if (!s.IsEnabled || !s.BirthdaySmsEnabled) return 0;

            var iranToday = IranNow.Date;
            var sent = 0;
            var reminders = await _db.BirthdayReminders.Where(b => b.IsActive).ToListAsync();
            var template = await GetTemplateAsync(SmsScenario.BirthdayReminder);
            if (template == null || !template.IsActive) return 0;

            foreach (var b in reminders)
            {
                // روز دقیق یادآوری = تولد منهای RemindDaysBefore (یا پیش‌فرض سراسری)
                int daysBefore = b.RemindDaysBefore > 0 ? b.RemindDaysBefore : s.BirthdayDaysBefore;
                DateTime nextBirthday = new DateTime(iranToday.Year, b.BirthDate.Month, b.BirthDate.Day);
                if (nextBirthday < iranToday) nextBirthday = nextBirthday.AddYears(1);
                var daysLeft = (nextBirthday - iranToday).Days;
                if (daysLeft != daysBefore) continue;

                string phone = NormalizePhone(b.PhoneNumber);
                if (phone.Length != 11) continue;

                var refKey = $"Birthday:{b.Id}:{nextBirthday:yyyy-MM-dd}";
                bool already = await _db.SmsAutoReminderLogs.AnyAsync(r => r.RefKey == refKey);
                if (already) continue;

                var vars = new Dictionary<string, string>
                {
                    { "PersonName", b.FirstName },
                    { "CustomerName", ToNameParts(b.FirstName + " " + b.LastName) }
                };
                var message = Render(template.BodyText, vars);

                _db.SmsAutoReminderLogs.Add(new SmsAutoReminderLog
                {
                    ReminderType = SmsReminderType.Birthday,
                    RefKey = refKey,
                    Phone = phone,
                    Message = message,
                    SentAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                await SendScenarioAsync(phone, SmsScenario.BirthdayReminder, vars, force: true);
                sent++;
            }
            return sent;
        }

        // ═══════════════ بازگشت مشتری + کد تخفیف خودکار (Hangfire روزانه) ═══════════════

        public async Task<int> SendWinBackDiscountsAsync()
        {
            var s = await GetSettingsAsync();
            if (!s.IsEnabled || !s.WinBackEnabled) return 0;

            var template = await GetTemplateAsync(SmsScenario.WinBackDiscount);
            if (template == null || !template.IsActive) return 0;

            var iranToday = IranNow.Date;
            var cutoff = DateTime.UtcNow.AddDays(-s.WinBackDays);
            var cutoffLimit = cutoff.AddDays(-1);

            // کاربرانی که آخرین خریدشان بین WinBackDays و WinBackDays+1 روز پیش بوده و امسال پیامک بازگشت نگرفته‌اند
            var lastPurchases = await _db.Orders
                .Where(o => o.UserId != null && o.PaymentStatus == PaymentStatus.Succeeded)
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Last = g.Max(o => o.CreatedAt) })
                .ToListAsync();

            int sent = 0;
            foreach (var lp in lastPurchases)
            {
                if (lp.Last > cutoff || lp.Last <= cutoffLimit) continue;

                var refKey = $"WinBack:{lp.UserId}:{iranToday:yyyy-MM}";
                if (await _db.SmsAutoReminderLogs.AnyAsync(r => r.RefKey == refKey)) continue;

                var user = await _userManager.FindByIdAsync(lp.UserId!);
                var phone = NormalizePhone(user?.PhoneNumber);
                if (phone.Length != 11) continue;

                // تولید کد تخفیف یکتا با پیشوند/عبارت قابل تعریف در تنظیمات
                var prefix = SanitizeCodePrefix(s.WinBackCodePrefix);
                // کد تخفیف با مولد امن ساخته میشود؛ کد قابل حدس، اجازه میداد کد بازگشت مشتری دیگری استفاده شود
                var code = prefix
                    + System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 10000).ToString(CultureInfo.InvariantCulture)
                    + iranToday.Day.ToString("00");
                var codeTitle = string.IsNullOrWhiteSpace(s.WinBackCodeTitle) ? "کد بازگشت مشتری (خودکار)" : s.WinBackCodeTitle.Trim();
                _db.DiscountCodes.Add(new DiscountCode
                {
                    Code = code,
                    Description = $"{codeTitle} - {user?.FullName ?? user?.UserName}",
                    DiscountType = DiscountType.Percentage,
                    DiscountValue = s.WinBackDiscountPercent,
                    MinimumOrderAmount = s.WinBackMinOrderAmount,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(s.WinBackDiscountValidityDays),
                    UsageLimit = 1,
                    IsActive = true
                });

                var vars = new Dictionary<string, string>
                {
                    { "CustomerName", ToNameParts(user?.FullName) },
                    { "DiscountCode", Fa(code) },
                    { "DiscountPercent", Fa(s.WinBackDiscountPercent) },
                    { "ValidityDays", Fa(s.WinBackDiscountValidityDays) }
                };
                var message = Render(template.BodyText, vars);

                _db.SmsAutoReminderLogs.Add(new SmsAutoReminderLog
                {
                    ReminderType = SmsReminderType.WinBack,
                    RefKey = refKey,
                    Phone = phone,
                    Message = message,
                    SentAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                await SendScenarioAsync(phone, SmsScenario.WinBackDiscount, vars, force: true);
                sent++;
            }
            return sent;
        }

        // ═══════════════ هشدار موجودی کم (Hangfire روزانه) ═══════════════

        public async Task<int> CheckLowStockAsync()
        {
            var s = await GetSettingsAsync();
            if (!s.IsEnabled || !s.LowStockAlertsEnabled) return 0;

            var refKey = $"LowStock:{IranNow:yyyy-MM-dd}";
            if (await _db.SmsAutoReminderLogs.AnyAsync(r => r.RefKey == refKey)) return 0;

            var lowItems = await _catalogDb.SweetItems
                .Where(i => i.IsActive && i.InventoryCount <= s.LowStockThreshold)
                .ToListAsync();
            if (lowItems.Count == 0) return 0;

            var names = string.Join("، ", lowItems.Take(5).Select(i => i.TitleFa));
            var vars = new Dictionary<string, string>
            {
                { "Products", names }
            };

            _db.SmsAutoReminderLogs.Add(new SmsAutoReminderLog
            {
                ReminderType = SmsReminderType.LowStockAlert,
                RefKey = refKey,
                Phone = "-",
                Message = names,
                SentAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            var template = await GetTemplateAsync(SmsScenario.LowStockAlert);
            if (template == null || !template.IsActive) return 0;
            // هشدار موجودی فقط به شماره‌های تعیین‌شده در تنظیمات (بدون پیامک به Owner)
            await SendToCustomPhonesAsync(s.LowStockAlertPhones, SmsScenario.LowStockAlert, vars, forceNow: true);
            return lowItems.Count;
        }

        /// <summary>هشدار موجودی با force ارسال می‌شود (تراکنشی برای مدیر).</summary>
        private Task SendToCustomPhonesAsync(string phones, SmsScenario scenario, Dictionary<string, string> vars, bool forceNow)
            => SendScenarioListAsync(phones, scenario, vars, forceNow);

        private async Task SendScenarioListAsync(string? phonesCsv, SmsScenario scenario, Dictionary<string, string>? vars, bool force)
        {
            if (string.IsNullOrWhiteSpace(phonesCsv)) return;
            foreach (var p in phonesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                await SendScenarioAsync(p, scenario, vars, SmsRecipientType.CustomPhones, force);
        }
    }
}
