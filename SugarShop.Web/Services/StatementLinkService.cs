using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// توکن لینک صورت‌حساب: ساخت لینک اختصاصی برای یک سفارش و اعتبارسنجی آن.
    /// توکن در دیتابیس ذخیره می‌شود (نه داخل خود آدرس)، پس کوتاه است — که برای هزینه پیامک مهم است —
    /// و می‌توان آن را باطل کرد. فقط «خواندن» سند همان سفارش را مجاز می‌کند.
    /// </summary>
    public class StatementLinkService
    {
        /// <summary>مدت اعتبار لینک پیامکی.</summary>
        public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(7);

        private readonly SugarShopSalesDbContext _db;

        public StatementLinkService(SugarShopSalesDbContext db) => _db = db;

        /// <summary>
        /// توکن معتبر سفارش را برمی‌گرداند؛ اگر لینک فعالی وجود داشته باشد همان بازگردانده می‌شود
        /// (تا با هر پرداخت جدید لینک‌های قبلی باطل نشوند) و در غیر این صورت لینک تازه ساخته می‌شود.
        /// </summary>
        public async Task<string?> GetOrCreateTokenAsync(int orderId, string? userId)
        {
            var now = DateTime.UtcNow;

            var existing = await _db.StatementLinks.AsNoTracking()
                .Where(l => l.OrderId == orderId && l.ExpiresAt > now)
                .OrderByDescending(l => l.Id)
                .FirstOrDefaultAsync();

            if (existing != null) return existing.Token;

            var link = new StatementLink
            {
                Token = CreateToken(),
                OrderId = orderId,
                UserId = userId,
                CreatedAt = now,
                ExpiresAt = now + LinkLifetime
            };

            _db.StatementLinks.Add(link);
            await _db.SaveChangesAsync();
            return link.Token;
        }

        /// <summary>توکن معتبر (وجود دارد و منقضی نشده) را برمی‌گرداند؛ در غیر این صورت null.</summary>
        public async Task<StatementLink?> FindUsableAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;

            var now = DateTime.UtcNow;
            return await _db.StatementLinks.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Token == token && l.ExpiresAt > now);
        }

        /// <summary>
        /// توکن فعال سفارش را <b>فقط می‌خواند</b> و هیچ لینک تازه‌ای نمی‌سازد.
        ///
        /// کاربردش بازگشت از درگاه است: مشتریِ بدون ورود (که از پیامک پرداخت کرده) باید سند
        /// نتیجه پرداخت را ببیند. اینجا عمداً توکن ساخته نمی‌شود تا وجود یک توکن، به هیچ درخواستی
        /// (حتی با trackId معتبر) اجازه دیدن سند ندهد؛ فقط همان سفارشی که لینک برایش ساخته شده بود.
        /// </summary>
        public async Task<string?> FindActiveTokenForOrderAsync(int orderId, string? userId)
        {
            var now = DateTime.UtcNow;

            var link = await _db.StatementLinks.AsNoTracking()
                .Where(l => l.OrderId == orderId && l.ExpiresAt > now)
                .OrderByDescending(l => l.Id)
                .FirstOrDefaultAsync();

            if (link == null) return null;

            // همان لایه دفاعی سند عمومی: اگر لینک به کاربری گره خورده باشد، سفارش باید همان کاربر باشد
            if (!string.IsNullOrEmpty(link.UserId) && link.UserId != userId) return null;

            return link.Token;
        }

        /// <summary>ثبت بازدید سند با لینک اختصاصی (شمارش برای سنجش اثر پیامک). خطا مهم نیست.</summary>
        public async Task RegisterOpenAsync(int linkId)
        {
            try
            {
                var link = await _db.StatementLinks.FirstOrDefaultAsync(l => l.Id == linkId);
                if (link == null) return;

                link.OpenCount++;
                link.LastOpenedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            catch
            {
                // آمار بازدید ارزش خطا دادن به مشتری را ندارد
            }
        }

        /// <summary>توکن کوتاه تصادفی و ایمن (۲۲ کاراکتر Base64Url از ۱۶ بایت آنتروپی).</summary>
        private static string CreateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
