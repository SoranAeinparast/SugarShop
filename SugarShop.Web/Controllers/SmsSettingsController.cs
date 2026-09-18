using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Domain.Entities.Sms;
using SugarShop.Web.Helpers;
using MD.PersianDateTime;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Web.Services;
using SugarShop.Web.Services.Sms;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Owner,Admin,OrderManager")]
    public class SmsSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _db;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly SmsService _sms;
        private readonly SmsIrClient _smsIr;
        private readonly ISmsQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public SmsSettingsController(
            SugarShopSalesDbContext db,
            SugarShopCatalogDbContext catalogDb,
            SmsService sms,
            SmsIrClient smsIr,
            ISmsQueue queue,
            IServiceScopeFactory scopeFactory,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _catalogDb = catalogDb;
            _sms = sms;
            _smsIr = smsIr;
            _queue = queue;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
        }

        private static string Fa(object? value)
        {
            var text = value?.ToString() ?? "";
            var map = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
            var sb = new StringBuilder();
            foreach (var c in text) sb.Append(c >= '0' && c <= '9' ? map[c - '0'] : c);
            return sb.ToString();
        }

        // ═══════════ داشبورد سامانه پیامکی ═══════════

        /// <summary>
        /// نوار وضعیت اعتبار sms.ir — تا مدیر مجبور نباشد به پنل sms.ir لاگین کند.
        /// ⚠️ واحد API اعتبار sms.ir «تعداد پیامک» است نه تومان (پنل هم 733 پیامک نشان می‌دهد).
        /// خطاها بی‌صدا نادیده گرفته می‌شوند تا صفحه تنظیمات هرگز به‌خاطر خطای درگاه کند/خراب نشود.
        /// </summary>
        private async Task LoadProviderCreditAsync(SmsSystemSetting s, bool forceRefresh = false)
        {
            ViewBag.CreditToman = (decimal?)null;
            ViewBag.CreditError = (string?)null;
            if (s.SandboxMode || string.IsNullOrWhiteSpace(s.ApiKey)) return;

            try
            {
                // در همین فرصت، وضعیت تحویل پیامک‌های اخیر نیز از درگاه استعلام می‌شود (ارزان و سریع)
                try { await _sms.SyncDeliveryStatesAsync(); } catch { }

                var credit = await _sms.GetProviderCreditAsync();
                if (credit.HasValue)
                {
                    ViewBag.CreditToman = credit.Value; // واحد: تعداد پیامک (در ویو «پیامک» نمایش داده می‌شود)
                }
                else
                {
                    ViewBag.CreditError = "دریافت اعتبار از sms.ir ناموفق بود";
                }
            }
            catch (Exception ex)
            {
                ViewBag.CreditError = "خطا در دریافت اعتبار: " + ex.Message;
            }
        }

        public async Task<IActionResult> Index()
        {
            var s = await _sms.GetSettingsAsync();
            // مرزهای ماه/روز بر مبنای وقت ایران محاسبه می‌شوند (ستون‌ها UTC هستند)
            var monthStart = IranClock.MonthStartUtc;
            ViewBag.MonthSent = await _db.SmsLogs.CountAsync(l => l.SentAt >= monthStart && l.Status == SmsSendStatus.Sent);
            ViewBag.MonthFailed = await _db.SmsLogs.CountAsync(l => l.SentAt >= monthStart && l.Status == SmsSendStatus.Failed);
            // هزینه ثبت‌شده بر حسب «تعداد پیامک» (واحد cost درگاه)
            ViewBag.MonthCost = await _db.SmsLogs.Where(l => l.SentAt >= monthStart).SumAsync(l => (decimal?)l.Cost) ?? 0;
            ViewBag.TodaySent = await _db.SmsLogs.CountAsync(l => l.SentAt >= IranClock.DayStartUtc && l.Status == SmsSendStatus.Sent);
            ViewBag.QueueCount = _queue.ApproximateCount;
            // پیامک‌هایی که درگاه اعتبار گرفته ولی مخابرات ارسال را خطا داده (deliveryState=6)
            ViewBag.DeliveryFailed = await _db.SmsLogs.CountAsync(l => l.SentAt >= monthStart && l.DeliveryState != null && l.DeliveryState >= 4);
            await LoadProviderCreditAsync(s);
            return View(s);
        }

        // ═══════════ اتصال به درگاه ═══════════

        [HttpGet]
        public async Task<IActionResult> Connection()
        {
            var s = await _sms.GetSettingsAsync();
            await LoadProviderCreditAsync(s);
            return View(s);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Connection(SmsSystemSetting model, string? newApiKey)
        {
            var s = await _sms.GetSettingsAsync();
            s.IsEnabled = model.IsEnabled;
            s.SenderNumber = (model.SenderNumber ?? "").Trim();
            s.SandboxMode = model.SandboxMode;
            if (!string.IsNullOrWhiteSpace(newApiKey)) s.ApiKey = newApiKey.Trim();
            s.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = "تنظیمات اتصال ذخیره شد.";
            return RedirectToAction(nameof(Connection));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestConnection(string? testPhone)
        {
            var s = await _sms.GetSettingsAsync();
            if (string.IsNullOrWhiteSpace(s.ApiKey))
            {
                TempData["Error"] = "ابتدا کلید API را ذخیره کنید.";
                return RedirectToAction(nameof(Connection));
            }                var lines = await _smsIr.GetLinesAsync(s.ApiKey);
                if (lines == null || lines.Count == 0)
                {
                    // کلید ممکن است معتبر باشد ولی مسیر خطوط در دسترس نباشد؛ اعتبار را بررسی می‌کنیم
                    var credit = await _smsIr.GetCreditAsync(s.ApiKey);
                    if (credit.HasValue)
                    {
                        TempData["Error"] = $"کلید API معتبر است (اعتبار: {credit:N0} پیامک) اما دریافت شماره خط ناموفق بود. شماره خط را دستی در فرم بالا وارد و ذخیره کنید.";
                    }
                else
                {
                    TempData["Error"] = "اتصال به sms.ir ناموفق بود؛ کلید API را بررسی کنید. (اگر مطمئن هستید کلید درست است، چند لحظه بعد دوباره تلاش کنید)";
                }
                return RedirectToAction(nameof(Connection));
            }

            ViewBag.Lines = lines;
            var line = string.IsNullOrWhiteSpace(s.SenderNumber) ? lines[0] : s.SenderNumber;

            if (!string.IsNullOrWhiteSpace(testPhone))
            {
                var phone = SmsService.NormalizePhone(testPhone);
                if (!SmsService.IsValidIranMobile(phone))
                {
                    TempData["Error"] = "شماره موبایل برای تست معتبر نیست.";
                }
                else
                {
                    // پیام تست مستقیم ارسال می‌شود (نه از صف) و موفقیت آن واقعی سنجیده می‌شود
                    var result = await _smsIr.SendBulkAsync(s.ApiKey, line, new[] { phone },
                        "اتصال سامانه پیامکی شیرینی‌سرا با موفقیت تست شد. ✅");
                    if (result.Success)
                    {
                        s.SandboxMode = false;           // اتصال سالم → ارسال واقعی فعال شود
                        s.SenderNumber = line;
                        s.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                        TempData["Success"] = $"تست موفق! پیامک به {phone} ارسال شد و سامانه از حالت آزمایشی خارج شد. شماره خط: {line}";
                    }
                    else
                    {
                        TempData["Error"] = "کلید معتبر است اما ارسال پیامک ناموفق بود: " + result.ErrorMessage;
                    }
                }
            }
            else
            {
                s.SenderNumber = line;
                await _db.SaveChangesAsync();
                TempData["Success"] = $"اتصال سالم است. خطوط فعال: {string.Join("، ", lines)}";
            }
            return RedirectToAction(nameof(Connection));
        }

        // ═══════════ مدیریت قالب‌ها ═══════════

        public async Task<IActionResult> Templates()
        {
            var templates = await _db.SmsTemplates.OrderBy(t => t.Scenario).ToListAsync();
            ViewBag.ScenarioNames = ScenarioNames();
            ViewBag.ScenarioVars = ScenarioVars();
            await LoadProviderCreditAsync(await _sms.GetSettingsAsync());
            return View(templates);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTemplate(int id, string bodyText, bool isActive)
        {
            var t = await _db.SmsTemplates.FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            if (string.IsNullOrWhiteSpace(bodyText))
            {
                TempData["Error"] = "متن پیامک نمی‌تواند خالی باشد.";
                return RedirectToAction(nameof(Templates));
            }
            t.BodyText = bodyText.Trim();
            t.IsActive = isActive;
            t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"قالب «{t.Title}» ذخیره شد.";
            return RedirectToAction(nameof(Templates));
        }

        // ═══════════ گزارش‌گیری ═══════════

        /// <summary>
        /// اثرسنجی پیامک‌های لینک‌دار: از سفارش‌هایی که پیامک لینک‌دار گرفتند، چند درصد
        /// لینک را باز کردند و از میانشان چند نفر پرداخت کردند.
        /// بازه پیش‌فرض ۳۰ روز است و از داده جدول SmsLinkTrackings می‌آید
        /// (ارسال‌ها لحظه صف شدن پیامک و بازدیدها لحظه کلیک مشتری ثبت می‌شوند).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Effect(int days = 30)
        {
            if (days != 7 && days != 30 && days != 90) days = 30;

            var report = await new SmsLinkReportService(_db).BuildAsync(days, DateTime.UtcNow);
            ViewBag.Days = days;
            // فقط برای نمایش: تاریخ شروع بازه به وقت ایران
            ViewBag.FromText = new PersianDateTime(report.FromIranDay).ToString("yyyy/MM/dd");
            await LoadProviderCreditAsync(await _sms.GetSettingsAsync());
            return View(report);
        }

        public async Task<IActionResult> Logs(int page = 1, int? status = null, string? phone = null)
        {
            const int pageSize = 30;
            var query = _db.SmsLogs.AsQueryable();
            if (status.HasValue) query = query.Where(l => (int)l.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var p = SmsService.NormalizePhone(phone);
                query = query.Where(l => l.PhoneNumber.Contains(p));
            }

            var total = await query.CountAsync();
            var logs = await query.OrderByDescending(l => l.SentAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalCount = total;
            ViewBag.StatusFilter = status;
            ViewBag.PhoneFilter = phone;
            ViewBag.StatusNames = StatusNames();
            await LoadProviderCreditAsync(await _sms.GetSettingsAsync());
            return View(logs);
        }

        public async Task<IActionResult> TestSms(string phone)
        {
            var ok = await _sms.SendScenarioAsync(phone, SmsScenario.TestMessage, force: true);
            TempData[ok ? "Success" : "Error"] = ok
                ? "پیامک تست در صف ارسال قرار گرفت."
                : "ارسال پیامک تست ناموفق بود (قالب فعال نیست یا شماره نامعتبر است).";
            return RedirectToAction(nameof(Logs));
        }

        /// <summary>به‌روزرسانی دستی وضعیت تحویل پیامک‌های اخیر از sms.ir.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncDelivery()
        {
            try
            {
                var (checkedCount, updatedCount) = await _sms.SyncDeliveryStatesAsync(force: true);
                TempData["Success"] = checkedCount == 0
                    ? "پیامک جدیدی برای استعلام وضعیت تحویل وجود ندارد."
                    : $"وضعیت {Fa(updatedCount)} پیامک از {Fa(checkedCount)} پیامک به‌روزرسانی شد.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "استعلام وضعیت تحویل ناموفق بود: " + ex.Message;
            }
            return RedirectToAction(nameof(Logs));
        }

        /// <summary>گزارش زنده ارسال‌های امروز مستقیماً از پنل sms.ir.</summary>
        public async Task<IActionResult> LiveReport()
        {
            var s = await _sms.GetSettingsAsync();
            ViewBag.LiveReport = await _sms.GetProviderLiveReportAsync();
            await LoadProviderCreditAsync(s);
            return View();
        }

        // ═══════════ تنظیمات پیشرفته ═══════════

        [HttpGet]
        public async Task<IActionResult> Advanced()
        {
            var s = await _sms.GetSettingsAsync();
            BuildRoleLists(s);
            await LoadProviderCreditAsync(s);
            return View(s);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Advanced(SmsSystemSetting model)
        {
            var s = await _sms.GetSettingsAsync();
            s.RespectQuietHours = model.RespectQuietHours;
            s.QuietStartHour = Math.Clamp(model.QuietStartHour, 0, 23);
            s.QuietEndHour = Math.Clamp(model.QuietEndHour, 0, 23);
            s.OtpPerPhonePerHour = Math.Max(1, model.OtpPerPhonePerHour);
            s.OtpPerIpPerHour = Math.Max(1, model.OtpPerIpPerHour);
            s.MaxSmsPerPhonePerDay = Math.Max(0, model.MaxSmsPerPhonePerDay);
            s.MonthlyBudgetToman = Math.Max(0, model.MonthlyBudgetToman);
            s.QueueBatchSize = Math.Clamp(model.QueueBatchSize, 1, 100);
            s.QueueDelaySeconds = Math.Clamp(model.QueueDelaySeconds, 0, 60);
            SmsProcessor.UpdateThrottle(s.QueueBatchSize, s.QueueDelaySeconds);

            s.StaffAlertsEnabled = model.StaffAlertsEnabled;
            var selectedRoles = Request.Form["alertRoles"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(r => r == "Admin" || r == "OrderManager" || r == "Chef") // Owner حذف شد
                .Distinct()
                .ToList();
            // اگر مدیر هیچ نقشی انتخاب نکرد، تنظیم قبلی حفظ می‌شود تا ذخیره تصادفی، اطلاع‌رسانی را خاموش نکند
            s.NewOrderAlertRoles = selectedRoles.Count > 0
                ? string.Join(",", selectedRoles)
                : (s.NewOrderAlertRoles ?? "Admin,OrderManager,Chef");
            s.NewOrderAlertOnlyWeighing = model.NewOrderAlertOnlyWeighing;
            s.NewOrderAlertPhones = (model.NewOrderAlertPhones ?? "").Trim();
            s.CustomCakeAlertsEnabled = model.CustomCakeAlertsEnabled;
            s.CustomCakeAlertPhones = (model.CustomCakeAlertPhones ?? "").Trim();

            s.RestockAlertsEnabled = model.RestockAlertsEnabled;
            s.LowStockAlertsEnabled = model.LowStockAlertsEnabled;
            s.LowStockThreshold = Math.Max(0, model.LowStockThreshold);
            s.LowStockAlertPhones = (model.LowStockAlertPhones ?? "").Trim();
            s.TicketNotificationsEnabled = model.TicketNotificationsEnabled;

            s.BirthdaySmsEnabled = model.BirthdaySmsEnabled;
            s.BirthdayDaysBefore = Math.Clamp(model.BirthdayDaysBefore, 0, 30);

            s.PaymentReminderEnabled = model.PaymentReminderEnabled;
            s.PaymentReminderDelayHours = Math.Clamp(model.PaymentReminderDelayHours, 1, 24 * 14);

            s.WinBackEnabled = model.WinBackEnabled;
            s.WinBackDays = Math.Max(7, model.WinBackDays);
            s.WinBackDiscountPercent = Math.Clamp(model.WinBackDiscountPercent, 1, 90);
            s.WinBackDiscountValidityDays = Math.Clamp(model.WinBackDiscountValidityDays, 1, 90);
            s.WinBackMinOrderAmount = Math.Max(0, model.WinBackMinOrderAmount);
            s.WinBackCodeTitle = (model.WinBackCodeTitle ?? "").Trim();
            s.WinBackCodePrefix = (model.WinBackCodePrefix ?? "").Trim();
            s.VipPurchaseThresholdToman = Math.Max(0, model.VipPurchaseThresholdToman);
            s.VipAutoSpecialOffers = model.VipAutoSpecialOffers;

            s.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = "تنظیمات پیشرفته ذخیره شد.";
            BuildRoleLists(s);
            return View(s);
        }

        private void BuildRoleLists(SmsSystemSetting s)
        {
            var selected = (s.NewOrderAlertRoles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // Owner عمداً در گزینه‌ها نیست — مدیر کل همیشه به‌صورت جداگانه (کیک سفارشی/هشدارها) مطلع می‌شود
            ViewBag.AlertRoles = new MultiSelectList(
                new[]
                {
                    new { Value = "Admin", Text = "Admin — ادمین" },
                    new { Value = "OrderManager", Text = "OrderManager — مدیر فروش" },
                    new { Value = "Chef", Text = "Chef — سرآشپز" }
                }, "Value", "Text", selected);
        }

        // ═══════════ ارسال انبوه تفکیک‌شده ═══════════

        public async Task<IActionResult> Campaigns()
        {
            var campaigns = await _db.SmsCampaigns.OrderByDescending(c => c.CreatedAt).Take(30).ToListAsync();
            var s = await _sms.GetSettingsAsync();
            ViewBag.VipThreshold = s.VipPurchaseThresholdToman;
            await LoadProviderCreditAsync(s);
            return View(campaigns);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCampaign()
        {
            var s = await _sms.GetSettingsAsync();
            ViewBag.Audiences = Audiences(s.VipPurchaseThresholdToman);
            return View(new SmsCampaign { Audience = SmsAudienceType.PurchasedLast90Days });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCampaign(string title, SmsAudienceType audience, string message)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                TempData["Error"] = "عنوان و متن پیام الزامی است.";
                return RedirectToAction(nameof(CreateCampaign));
            }

            var phones = await ResolveAudienceAsync(audience);
            if (phones.Count == 0)
            {
                TempData["Error"] = "هیچ مخاطبی برای این تفکیک یافت نشد.";
                return RedirectToAction(nameof(CreateCampaign));
            }

            var campaign = new SmsCampaign
            {
                Title = title.Trim(),
                Audience = audience,
                Message = message.Trim(),
                TotalRecipients = phones.Count,
                Status = SmsCampaignStatus.Sending,
                CreatedByUserId = _userManager.GetUserId(User)
            };
            _db.SmsCampaigns.Add(campaign);
            await _db.SaveChangesAsync();

            _db.SmsCampaignRecipients.AddRange(phones.Select(p => new SmsCampaignRecipient
            {
                CampaignId = campaign.Id,
                Phone = p.Phone,
                RecipientName = p.Name
            }));
            await _db.SaveChangesAsync();

            // ارسال در پس‌زمینه با فاصله زمانی — کندی سایت صفر؛ وضعیت هر گیرنده ثبت می‌شود
            var scopeFactory = _scopeFactory;
            var campaignId = campaign.Id;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db2 = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
                    var sms2 = scope.ServiceProvider.GetRequiredService<SmsService>();
                    var s2 = await sms2.GetSettingsAsync();

                    foreach (var r in await db2.SmsCampaignRecipients.Where(x => x.CampaignId == campaignId).ToListAsync())
                    {
                        var vars = new Dictionary<string, string>
                        {
                            { "CustomerName", (r.RecipientName ?? "مشتری").Split(' ')[0] },
                            { "Message", campaign.Message },
                            { "SiteName", "شیرینی سرا" }
                        };
                        var queued = await sms2.SendScenarioAsync(r.Phone, SmsScenario.SpecialOffer, vars, SmsRecipientType.Customer);
                        r.Status = queued ? SmsSendStatus.Sent : SmsSendStatus.Skipped;
                        db2.SmsCampaignRecipients.Update(r);
                        await db2.SaveChangesAsync();
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, s2.QueueDelaySeconds)));
                    }

                    var camp = await db2.SmsCampaigns.FindAsync(campaignId);
                    if (camp != null)
                    {
                        camp.Status = SmsCampaignStatus.Completed;
                        camp.CompletedAt = DateTime.UtcNow;
                        await db2.SaveChangesAsync();
                    }
                }
                catch
                {
                    // کمپین در حالت Sending می‌ماند و در گزارش قابل مشاهده است
                }
            });

            TempData["Success"] = $"کمپین با {Fa(campaign.TotalRecipients)} گیرنده در صف ارسال قرار گرفت.";
            return RedirectToAction(nameof(Campaigns));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelCampaign(int id)
        {
            var c = await _db.SmsCampaigns.FindAsync(id);
            if (c == null) return NotFound();
            c.Status = SmsCampaignStatus.Cancelled;
            await _db.SaveChangesAsync();
            TempData["Success"] = "کمپین لغو شد.";
            return RedirectToAction(nameof(Campaigns));
        }

        private async Task<List<(string Phone, string? Name)>> ResolveAudienceAsync(SmsAudienceType audience)
        {
            var result = new List<(string, string?)>();
            var allUsers = await _userManager.Users
                .Where(u => u.PhoneNumber != null && u.PhoneNumber != "")
                .Select(u => new { u.Id, u.FullName, u.PhoneNumber })
                .ToListAsync();

            if (audience == SmsAudienceType.AllCustomers || audience == SmsAudienceType.SmsSubscribers)
            {
                result.AddRange(allUsers.Select(u => (u.PhoneNumber!, (string?)u.FullName)));
                return result;
            }

            var paidByUser = await _db.Orders
                .Where(o => o.UserId != null && o.PaymentStatus == PaymentStatus.Succeeded)
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Last = g.Max(o => o.CreatedAt), Total = g.Sum(o => o.FinalTotalAmount ?? o.TotalAmountSnapshot) })
                .ToListAsync();

            var userMap = allUsers.ToDictionary(u => u.Id, u => u);
            var s = await _sms.GetSettingsAsync();

            switch (audience)
            {
                case SmsAudienceType.PurchasedLast90Days:
                    foreach (var o in paidByUser.Where(x => x.Last >= DateTime.UtcNow.AddDays(-90)))
                        if (userMap.TryGetValue(o.UserId!, out var u)) result.Add((u.PhoneNumber!, u.FullName));
                    break;
                case SmsAudienceType.PurchasedLast180Days:
                    foreach (var o in paidByUser.Where(x => x.Last >= DateTime.UtcNow.AddDays(-180)))
                        if (userMap.TryGetValue(o.UserId!, out var u)) result.Add((u.PhoneNumber!, u.FullName));
                    break;
                case SmsAudienceType.VipCustomers:
                    foreach (var o in paidByUser.Where(x => x.Total >= s.VipPurchaseThresholdToman))
                        if (userMap.TryGetValue(o.UserId!, out var u)) result.Add((u.PhoneNumber!, u.FullName));
                    break;
                case SmsAudienceType.NoPurchase180Days:
                    foreach (var o in paidByUser.Where(x => x.Last < DateTime.UtcNow.AddDays(-180)))
                        if (userMap.TryGetValue(o.UserId!, out var u)) result.Add((u.PhoneNumber!, u.FullName));
                    break;
                case SmsAudienceType.BirthdaysThisMonth:
                    var month = DateTime.UtcNow.AddHours(3.5).Month;
                    var bdays = await _db.BirthdayReminders.Where(b => b.IsActive && b.BirthDate.Month == month && b.PhoneNumber != null).ToListAsync();
                    result.AddRange(bdays.Select(b => (b.PhoneNumber!, (string?)(b.FirstName + " " + b.LastName))));
                    break;
            }
            return result.DistinctBy(x => SmsService.NormalizePhone(x.Item1)).ToList();
        }

        // ═══════════ نام‌های فارسی ═══════════

        public static Dictionary<SmsScenario, string> ScenarioNames() => new()
        {
            { SmsScenario.Otp, "کد ورود یکبار مصرف" },
            { SmsScenario.PasswordResetOtp, "کد بازیابی رمز عبور" },
            { SmsScenario.RegisterWelcome, "خوش‌آمدگویی ثبت‌نام" },
            { SmsScenario.OrderPlacedCustomer, "ثبت سفارش — مشتری" },
            { SmsScenario.OrderPlacedStaff, "ثبت سفارش — کارکنان" },
            { SmsScenario.WeighingReadyCustomer, "آماده پرداخت (پس از وزن‌کشی)" },
            { SmsScenario.PaymentConfirmedCustomer, "تأیید پرداخت" },
            { SmsScenario.PaymentStatementCustomer, "صورت‌حساب پرداخت (لینک)" },
            { SmsScenario.PaymentReminderCustomer, "یادآوری پرداخت (پس از وزن‌کشی)" },
            { SmsScenario.OrderStatusChangedCustomer, "تغییر وضعیت سفارش" },
            { SmsScenario.CustomCakeStatusCustomer, "وضعیت کیک سفارشی" },
            { SmsScenario.TicketReceivedCustomer, "دریافت تیکت" },
            { SmsScenario.TicketAnsweredCustomer, "پاسخ تیکت" },
            { SmsScenario.BirthdayReminder, "یادآوری تولد" },
            { SmsScenario.WinBackDiscount, "بازگشت مشتری + کد تخفیف" },
            { SmsScenario.RestockAvailable, "محصول موجود شد" },
            { SmsScenario.LowStockAlert, "هشدار موجودی کم" },
            { SmsScenario.SpecialOffer, "اطلاع‌رسانی ویژه (انبوه)" },
            { SmsScenario.TestMessage, "پیامک تست" }
        };

        public static Dictionary<SmsScenario, string> ScenarioVars() => new()
        {
            { SmsScenario.Otp, "{Code} {SiteName}" },
            { SmsScenario.PasswordResetOtp, "{Code} {SiteName}" },
            { SmsScenario.RegisterWelcome, "{CustomerName} {SiteName}" },
            { SmsScenario.OrderPlacedCustomer, "{CustomerName} {OrderCode} {Amount} {SiteName}" },
            { SmsScenario.OrderPlacedStaff, "{CustomerName} {OrderCode} {Amount} {SiteName}" },
            { SmsScenario.WeighingReadyCustomer, "{CustomerName} {OrderCode} {Amount} {OrderLink} {StatementLink} {SiteName}" },
            { SmsScenario.PaymentConfirmedCustomer, "{CustomerName} {OrderCode} {Amount} {SiteName}" },
            { SmsScenario.PaymentStatementCustomer, "{CustomerName} {OrderCode} {Amount} {StatementLink} {SiteName}" },
            { SmsScenario.PaymentReminderCustomer, "{CustomerName} {OrderCode} {Amount} {OrderLink} {StatementLink} {SiteName}" },
            { SmsScenario.OrderStatusChangedCustomer, "{CustomerName} {OrderCode} {Status} {SiteName}" },
            { SmsScenario.CustomCakeStatusCustomer, "{CustomerName} {CakeCode} {Status} {SiteName}" },
            { SmsScenario.TicketReceivedCustomer, "{CustomerName} {TicketId} {SiteName}" },
            { SmsScenario.TicketAnsweredCustomer, "{CustomerName} {TicketId} {SiteName}" },
            { SmsScenario.BirthdayReminder, "{CustomerName} {PersonName} {SiteName}" },
            { SmsScenario.WinBackDiscount, "{CustomerName} {DiscountCode} {DiscountPercent} {ValidityDays} {SiteName}" },
            { SmsScenario.RestockAvailable, "{CustomerName} {ProductName} {SiteName}" },
            { SmsScenario.LowStockAlert, "{Products} {SiteName}" },
            { SmsScenario.SpecialOffer, "{CustomerName} {Message} {SiteName}" },
            { SmsScenario.TestMessage, "{SiteName}" }
        };

        public static Dictionary<int, string> StatusNames() => new()
        {
            { (int)SmsSendStatus.Pending, "در انتظار" },
            { (int)SmsSendStatus.Sent, "ارسال موفق" },
            { (int)SmsSendStatus.Failed, "ناموفق" },
            { (int)SmsSendStatus.BlockedQuietHours, "مسدود — ساعات سکوت" },
            { (int)SmsSendStatus.BlockedRateLimit, "مسدود — سقف ارسال" },
            { (int)SmsSendStatus.BlockedDisabled, "مسدود — غیرفعال/بودجه" },
            { (int)SmsSendStatus.Skipped, "رد شده" }
        };

        public static SelectList Audiences(int vipThreshold) => new(
            new[]
            {
                new { Value = (int)SmsAudienceType.PurchasedLast90Days, Text = "خرید کرده در ۳ ماه گذشته (پیشنهادی — نرخ تبدیل بالا)" },
                new { Value = (int)SmsAudienceType.PurchasedLast180Days, Text = "خرید کرده در ۶ ماه گذشته" },
                new { Value = (int)SmsAudienceType.VipCustomers, Text = $"مشتریان VIP (خرید بیش از {Fa(vipThreshold)} تومان)" },
                new { Value = (int)SmsAudienceType.NoPurchase180Days, Text = "بیش از ۶ ماه خرید نکرده‌اند (فعال‌سازی مجدد)" },
                new { Value = (int)SmsAudienceType.AllCustomers, Text = "همه مشتریان دارای شماره موبایل" },
                new { Value = (int)SmsAudienceType.BirthdaysThisMonth, Text = "تولدهای این ماه" }
            }, "Value", "Text");
    }
}
