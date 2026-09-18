using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;

namespace SugarShop.Web.Services
{
    /// <summary>یک ردیف گزارش اثرسنجی برای یک نوع لینک پیامکی.</summary>
    public class SmsLinkReportRow
    {
        public SmsLinkKind Kind { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>تعداد سفارش‌هایی که پیامک لینک‌دار گرفتند (مخرج همه‌ی درصدها).</summary>
        public int Sent { get; set; }

        /// <summary>چند سفارش، لینک را حداقل یک‌بار باز کردند.</summary>
        public int Opened { get; set; }

        /// <summary>جمع بازدیدها (هر لینک ممکن است چند بار باز شود).</summary>
        public int OpenCount { get; set; }

        /// <summary>سفارش‌هایی که بعد از این پیامک، پرداخت موفق داشتند.</summary>
        public int Paid { get; set; }

        /// <summary>میانگین دقیقه تا اولین بازدید (برای پیامک‌های بازشده).</summary>
        public double? AvgMinutesToOpen { get; set; }

        public double OpenRate => Sent == 0 ? 0 : Math.Round(Opened * 100.0 / Sent, 1);
        public double PayRateOfSent => Sent == 0 ? 0 : Math.Round(Paid * 100.0 / Sent, 1);
        public double PayRateOfOpened => Opened == 0 ? 0 : Math.Round(Paid * 100.0 / Opened, 1);
        public double OpensPerLink => Opened == 0 ? 0 : Math.Round(OpenCount * 1.0 / Opened, 2);
    }

    /// <summary>نقطه‌ی نمودار روزانه: چند پیامک لینک‌دار رفت و چند تا باز شد.</summary>
    public class SmsLinkReportDay
    {
        public DateTime Day { get; set; }
        public int Sent { get; set; }
        public int Opened { get; set; }
        public double OpenRate => Sent == 0 ? 0 : Math.Round(Opened * 100.0 / Sent, 1);
    }

    public class SmsLinkReportModel
    {
        public int Days { get; set; }

        /// <summary>شروع بازه به وقت ایران (برای نمایش).</summary>
        public DateTime FromIranDay { get; set; }

        /// <summary>شروع بازه به UTC (همان مرزی که روی ستون‌های دیتابیس اعمال شده است).</summary>
        public DateTime FromUtc { get; set; }

        public DateTime ToUtc { get; set; }
        public List<SmsLinkReportRow> Rows { get; set; } = new();
        public List<SmsLinkReportDay> Daily { get; set; } = new();

        /// <summary>
        /// بازدیدهایی که ارسال متناظرشان ثبت نشده (پیامک‌های ارسال‌شده پیش از فعال شدن اثرسنجی).
        /// جداگانه نشان داده می‌شود تا در مخرج درصدها حساب نشود.
        /// </summary>
        public int UnattributedOpens { get; set; }

        public SmsLinkReportRow? WaitingPayment => Rows.FirstOrDefault(r => r.Kind == SmsLinkKind.WaitingPayment);
        public SmsLinkReportRow? Statement => Rows.FirstOrDefault(r => r.Kind == SmsLinkKind.Statement);
    }

    /// <summary>
    /// پاسخ به سؤال «پیامک‌ها چقدر اثر دارند؟»: از میان سفارش‌هایی که پیامک لینک‌دار گرفتند،
    /// چند درصد لینک را باز کردند و از میان آن‌ها چند نفر پرداخت کردند.
    ///
    /// مخرج «ارسال‌شده در بازه» است (کوهورت) و صورت کسر «باز شده / پرداخت بعد از همان پیامک».
    /// پرداخت با رکوردهای موفق جدول Payments سنجیده می‌شود، نه با وضعیت فعلی سفارش؛
    /// چون سفارش مرحله‌ای می‌تواند چند پرداخت داشته باشد.
    /// </summary>
    public class SmsLinkReportService
    {
        private readonly SugarShopSalesDbContext _db;

        public SmsLinkReportService(SugarShopSalesDbContext db) => _db = db;

        public async Task<SmsLinkReportModel> BuildAsync(int days, DateTime utcNow)
        {
            if (days <= 0) days = 30;

            // مرز بازه بر مبنای روز ایران محاسبه می‌شود (نه روز UTC)، چون مدیر «۷ روز اخیر»
            // را با تقویم خودش می‌سنجد؛ ستون‌های دیتابیس UTC هستند و تبدیل همان اول انجام می‌شود.
            var fromIranDay = IranClock.IranDayOf(utcNow).AddDays(-(days - 1));
            var from = IranClock.DayStartUtcFor(fromIranDay);

            var model = new SmsLinkReportModel
            {
                Days = days,
                FromIranDay = fromIranDay,
                FromUtc = from,
                ToUtc = utcNow
            };

            var tracked = await _db.SmsLinkTrackings.AsNoTracking()
                .Where(t => (t.SentAt != null && t.SentAt >= from) || (t.FirstOpenedAt != null && t.FirstOpenedAt >= from))
                .ToListAsync();

            model.UnattributedOpens = tracked.Count(t => t.SentAt == null && t.FirstOpenedAt != null);

            // کوهورت: فقط پیامک‌هایی که ارسالشان در همین بازه ثبت شده است
            var sent = tracked.Where(t => t.SentAt.HasValue).ToList();
            var orderIds = sent.Select(t => t.OrderId).Distinct().ToList();

            // اولین پرداخت موفق هر سفارش (زمان درخواست درگاه ≈ زمان پرداخت)
            var paidTimes = new Dictionary<int, DateTime>();
            if (orderIds.Count > 0)
            {
                var payments = await _db.Payments.AsNoTracking()
                    .Where(p => orderIds.Contains(p.OrderId) && p.PaymentStatus == PaymentStatus.Succeeded)
                    .Select(p => new { p.OrderId, p.CreatedAt })
                    .ToListAsync();

                paidTimes = payments
                    .GroupBy(p => p.OrderId)
                    .ToDictionary(g => g.Key, g => g.Min(p => p.CreatedAt));
            }

            foreach (var kind in new[] { SmsLinkKind.WaitingPayment, SmsLinkKind.Statement })
            {
                var rows = sent.Where(t => t.Kind == kind).ToList();
                var opened = rows.Where(t => t.FirstOpenedAt.HasValue).ToList();

                var paid = rows.Count(t => IsPaidAfter(t, paidTimes));
                var toOpen = opened
                    .Where(t => t.FirstOpenedAt >= t.SentAt)
                    .Select(t => (t.FirstOpenedAt!.Value - t.SentAt!.Value).TotalMinutes)
                    .ToList();

                var row = new SmsLinkReportRow
                {
                    Kind = kind,
                    Title = kind == SmsLinkKind.WaitingPayment ? "لینک پرداخت سفارش" : "لینک صورت‌حساب",
                    Description = kind == SmsLinkKind.WaitingPayment
                        ? "پیامک «سفارش آماده پرداخت است» با آدرس کوتاه /p/{id}"
                        : "پیامک «صورت‌حساب پرداخت آماده است» با لینک اختصاصی /s/{token}",
                    Sent = rows.Count,
                    Opened = opened.Count,
                    OpenCount = opened.Sum(t => t.OpenCount),
                    Paid = paid,
                    AvgMinutesToOpen = toOpen.Count > 0 ? Math.Round(toOpen.Average(), 1) : (double?)null
                };

                model.Rows.Add(row);
            }

            var dailyFromIran = IranClock.IranDayOf(utcNow).AddDays(-29);
            model.Daily = sent
                .Where(t => IranClock.IranDayOf(t.SentAt!.Value) >= dailyFromIran)
                .GroupBy(t => IranClock.IranDayOf(t.SentAt!.Value))
                .OrderBy(g => g.Key)
                .Select(g => new SmsLinkReportDay
                {
                    Day = g.Key,
                    Sent = g.Count(),
                    Opened = g.Count(t => t.FirstOpenedAt.HasValue)
                })
                .ToList();

            return model;
        }

        /// <summary>آیا بعد از همین پیامک، پرداخت موفقی برای آن سفارش ثبت شده است؟</summary>
        private static bool IsPaidAfter(SmsLinkTracking tracking, Dictionary<int, DateTime> paidTimes)
            => tracking.SentAt.HasValue
               && paidTimes.TryGetValue(tracking.OrderId, out var paidAt)
               && paidAt >= tracking.SentAt.Value;
    }
}
