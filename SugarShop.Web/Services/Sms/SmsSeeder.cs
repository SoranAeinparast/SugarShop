using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities.Sms;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services.Sms
{
    /// <summary>درج قالب‌های پیش‌فرض پیامک (پیش‌فرض‌های هوشمند) در اولین اجرا.</summary>
    public static class SmsSeeder
    {
        public static async Task SeedAsync(IServiceScopeFactory scopeFactory, ILogger logger)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

                // تنظیمات سراسری (Id به‌صورت identity خودکار ۱ می‌شود)
                if (!await db.SmsSystemSettings.AnyAsync())
                {
                    db.SmsSystemSettings.Add(new SmsSystemSetting
                    {
                        ApiKey = "YMciNjJN0zhcZlvBNb8RK4n78ZN0Yo8wp3Bt9UsNa6tenIQo",
                        SenderNumber = "",
                        SandboxMode = true // تا وقتی مدیر «تست اتصال» را نزده، واقعی ارسال نشود
                    });
                }

                // قالب‌های پیش‌فرض برای هر سناریو (اگر از قبل در جدول نیستند)
                var defaults = new (SmsScenario Scenario, string Title, string Body)[]
                {
                    (SmsScenario.Otp, "کد ورود یکبار مصرف",
                        "{SiteName}\nکد ورود شما: {Code}\nاین کد تا ۲ دقیقه اعتبار دارد."),
                    (SmsScenario.PasswordResetOtp, "کد بازیابی رمز عبور",
                        "{SiteName}\nکد بازیابی رمز عبور شما: {Code}\nاین کد تا ۲ دقیقه اعتبار دارد."),
                    (SmsScenario.RegisterWelcome, "خوش‌آمدگویی ثبت‌نام",
                        "{CustomerName} عزیز، به {SiteName} خوش آمدید! 🌸\nشیرینی‌های تازه ما در انتظار شماست."),
                    (SmsScenario.OrderPlacedCustomer, "ثبت سفارش جدید (مشتری)",
                        "{CustomerName} عزیز، سفارش {OrderCode} شما در {SiteName} ثبت شد. ✅\nپس از بررسی، برای پرداخت اطلاع‌رسانی می‌کنیم."),
                    (SmsScenario.OrderPlacedStaff, "ثبت سفارش جدید (کارکنان)",
                        "سفارش جدید {OrderCode}\nمشتری: {CustomerName}\nمبلغ: {Amount} تومان\nلطفاً در پنل بررسی کنید. — {SiteName}"),
                    (SmsScenario.WeighingReadyCustomer, "آماده پرداخت (پس از وزن‌کشی)",
                        "{CustomerName} عزیز، سفارش {OrderCode} وزن‌کشی شد. ⚖️\nمبلغ {Amount} تومان.\n{OrderLink}\n— {SiteName}"),
                    (SmsScenario.PaymentConfirmedCustomer, "تأیید پرداخت",
                        "{CustomerName} عزیز، پرداخت سفارش {OrderCode} به مبلغ {Amount} تومان با موفقیت تأیید شد. 🎉\n{SiteName}"),
                    (SmsScenario.PaymentStatementCustomer, "صورت‌حساب پرداخت (لینک)",
                        "{CustomerName} عزیز، صورت‌حساب پرداخت سفارش {OrderCode} آماده است. 🧾\n{StatementLink}\n{SiteName}"),
                    (SmsScenario.PaymentReminderCustomer, "یادآوری پرداخت (پس از وزن‌کشی)",
                        "{CustomerName} عزیز، یادآوری سفارش {OrderCode} 🧾\nمبلغ {Amount} تومان هنوز پرداخت نشده است.\n{OrderLink}\n— {SiteName}"),
                    (SmsScenario.OrderStatusChangedCustomer, "تغییر وضعیت سفارش",
                        "{CustomerName} عزیز، وضعیت سفارش {OrderCode}: «{Status}» شد.\n{SiteName}"),
                    (SmsScenario.CustomCakeStatusCustomer, "وضعیت کیک سفارشی",
                        "{CustomerName} عزیز، وضعیت سفارش کیک سفارشی {CakeCode}: «{Status}» شد.\n{SiteName}"),
                    (SmsScenario.TicketReceivedCustomer, "دریافت تیکت",
                        "{CustomerName} عزیز، تیکت شماره {TicketId} شما دریافت شد و به‌زودی پاسخ داده می‌شود. 📩\n{SiteName}"),
                    (SmsScenario.TicketAnsweredCustomer, "پاسخ تیکت",
                        "{CustomerName} عزیز، تیکت {TicketId} شما پاسخ داده شد. لطفاً به پروفایل خود مراجعه کنید. ✅\n{SiteName}"),
                    (SmsScenario.BirthdayReminder, "یادآوری تولد (دعوت به سفارش کیک)",
                        "{CustomerName} عزیز، تولد «{PersonName}» نزدیک است! 🎂\nسفارش کیک تولد را همین امروز در {SiteName} ثبت کنید تا به‌موقع برسد."),
                    (SmsScenario.WinBackDiscount, "بازگشت مشتری + کد تخفیف",
                        "{CustomerName} عزیز، دلمان برایتان تنگ شده! 💛\nبا کد {DiscountCode} تا {ValidityDays} روز {DiscountPercent}٪ تخفیف بگیرید. — {SiteName}"),
                    (SmsScenario.RestockAvailable, "موجود شد",
                        "{CustomerName} عزیز، «{ProductName}» دوباره موجود شد! 🎉\nهمین حالا در {SiteName} سفارش دهید."),
                    (SmsScenario.LowStockAlert, "هشدار موجودی کم (مدیر)",
                        "هشدار موجودی کم در {SiteName}:\n{Products}\nلطفاً بررسی کنید."),
                    (SmsScenario.SpecialOffer, "اطلاع‌رسانی ویژه (انبوه)",
                        "{CustomerName} عزیز، {SiteName}\n{Message}"),
                    (SmsScenario.TestMessage, "پیامک تست پنل",
                        "این یک پیامک آزمایشی از سامانه پیامکی {SiteName} است. ✅")
                };

                foreach (var (scenario, title, body) in defaults)
                {
                    if (await db.SmsTemplates.AnyAsync(t => t.Scenario == scenario)) continue;
                    db.SmsTemplates.Add(new SmsTemplate
                    {
                        Scenario = scenario,
                        Title = title,
                        BodyText = body,
                        IsActive = true,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                await db.SaveChangesAsync();

                // نصب‌های قبلی، قالب «آماده پرداخت» را بدون لینک دارند؛ یک‌بار و فقط یک‌بار لینک را اضافه می‌کنیم
                await SmsTemplateUpgrader.AppendWaitingLinkAsync(db, logger);

                logger.LogInformation("SMS seeding verified (settings + {Count} templates).", defaults.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SMS seeding failed. The site continues without SMS.");
            }
        }
    }
}
