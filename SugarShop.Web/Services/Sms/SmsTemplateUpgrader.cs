using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities.Sms;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services.Sms
{
    /// <summary>
    /// ارتقای قالب‌های پیامکیِ نصب‌شده‌ی قبلی. Seeder فقط قالب‌های غایب را اضافه می‌کند و
    /// متن قالب‌های موجود را «بازنویسی نمی‌کند» (تا ویرایش‌های مدیر از دست نرود)؛ پس تغییرات
    /// لازم در متن پیش‌فرض، اینجا به‌صورت یک‌باره و محافظت‌شده اعمال می‌شوند.
    /// </summary>
    public static class SmsTemplateUpgrader
    {
        /// <summary>
        /// اگر قالب «آماده پرداخت (پس از وزن‌کشی)» هیچ لینکی ندارد، خط <c>{OrderLink}</c> را به آن اضافه کن
        /// تا مشتری با یک کلیک به همان سفارش برسد.
        /// محافظت‌ها: اگر مدیر خودش لینک/متغیر لینک گذاشته باشد، دست نمی‌زنیم.
        /// </summary>
        public static async Task<bool> AppendWaitingLinkAsync(SugarShopSalesDbContext db, ILogger logger)
        {
            var template = await db.SmsTemplates
                .FirstOrDefaultAsync(t => t.Scenario == SmsScenario.WeighingReadyCustomer);

            if (template == null) return false;

            if (template.BodyText.Contains("{OrderLink}", StringComparison.OrdinalIgnoreCase)
                || template.BodyText.Contains("{StatementLink}", StringComparison.OrdinalIgnoreCase)
                || template.BodyText.Contains("http", StringComparison.OrdinalIgnoreCase))
            {
                return false; // قبلاً لینک دارد (یا خودِ مدیر متن را تغییر داده است)
            }

            template.BodyText = template.BodyText.TrimEnd() + Environment.NewLine + "{OrderLink}";
            template.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            logger.LogInformation("قالب پیامک «آماده پرداخت (پس از وزن‌کشی)» با متغیر لینک سفارش به‌روزرسانی شد.");
            return true;
        }
    }
}
