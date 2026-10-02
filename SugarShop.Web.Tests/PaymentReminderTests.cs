using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Domain.Entities.Sms;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services;
using SugarShop.Web.Services.Sms;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// 📨 قفل‌کردن «یادآوری خودکار پرداخت» — خطرناک‌ترین اتوماسیون برنامه.
///
/// این کار زمان‌بندی‌شده خودش به مشتری پیامک می‌فرستد؛ پس باید دقیقاً ثابت شود که:
///   • فقط سفارش واجد شرط (وزن‌کشی‌شده، پرداخت‌نشده، لینک پرداخت قدیمی، بدون یادآوری قبلی) انتخاب می‌شود،
///   • هر سفارش فقط **یک‌بار** یادآوری می‌گیرد،
///   • مبلغ و لینک داخل پیامک با منبع رسمی مبلغ (OrderPricingService) و لینک بدون‌ورود یکی است،
///   • اگر تنظیمات/قالب/آدرس پایه مشکل داشته باشد، پیامکی نمی‌رود و سفارش برای اجرای بعدی «علامت» نمی‌خورد.
///
/// هیچ پیامک واقعی ارسال نمی‌شود: سرویس فقط پیام را در صف (SmsOutboxItems) می‌گذارد و در محیط
/// «Testing» پردازنده‌ی صف خاموش است. آزمون همان ردیف صف را می‌خواند.
/// </summary>
public class PaymentReminderTests : IClassFixture<AppModeWebFactory>
{
    private const string CustomerPhone = "09123456789";
    private const string BaseUrl = "https://shop.autotest.local";
    private const string TemplateBody =
        "{CustomerName} عزیز، سفارش {OrderCode} با مبلغ {Amount} تومان هنوز پرداخت نشده است.\n{OrderLink}\n— {SiteName}";

    private readonly AppModeWebFactory _factory;

    public PaymentReminderTests(AppModeWebFactory factory) => _factory = factory;

    // ─────────────────────────────────────────────
    // حالت شاد: یک یادآوری، با مبلغ و لینک درست
    // ─────────────────────────────────────────────
    [Fact]
    public async Task Eligible_order_gets_exactly_one_reminder_with_the_right_amount_and_link()
    {
        await ConfigureAsync();
        var order = await CreateOrderAsync("AUTOTEST-REM-A", 250_000m, linkSentAt: DateTime.UtcNow.AddHours(-30));

        await RunAsync();

        var message = await SingleQueuedMessageAsync(order.OrderCode);
        Assert.Contains("AUTOTEST-REM-A", message);
        Assert.Contains("/s/", message);
        Assert.Contains("?pay=1", message);
        Assert.Contains(BaseUrl, message);
        Assert.Contains(Digits("250000"), Digits(message));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
        var reloaded = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        Assert.NotNull(reloaded.PaymentReminderSentAt);
    }

    [Fact]
    public async Task Second_run_does_not_send_the_reminder_again()
    {
        await ConfigureAsync();
        var order = await CreateOrderAsync("AUTOTEST-REM-B", 180_000m, linkSentAt: DateTime.UtcNow.AddHours(-30));

        await RunAsync();
        await RunAsync();

        // اجرای دوم نباید پیامک تکراری بفرستد
        Assert.Equal(1, await QueuedCountAsync(order.OrderCode));
    }

    [Fact]
    public async Task Reminder_amount_follows_the_official_pricing_source()
    {
        // سفارش با محصول ۱۵۰٬۰۰۰ + پیک ۳۰٬۰۰۰ − تخفیف ۲۰٬۰۰۰ = ۱۶۰٬۰۰۰
        await ConfigureAsync();
        var order = await CreateOrderAsync("AUTOTEST-REM-C", 150_000m,
            linkSentAt: DateTime.UtcNow.AddHours(-30), mutate: o =>
            {
                o.DeliveryFeeSnapshot = 30_000m;
                o.DiscountAmountSnapshot = 20_000m;
            });

        await RunAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
        var pricing = await scope.ServiceProvider.GetRequiredService<OrderPricingService>()
            .ComputeAsync(await db.Orders.Include(o => o.Items).AsNoTracking().FirstAsync(o => o.Id == order.Id));

        Assert.Equal(160_000m, pricing.GrandTotal);
        var message = await SingleQueuedMessageAsync(order.OrderCode);
        Assert.Contains(Digits("160000"), Digits(message));
        Assert.DoesNotContain(Digits("150000"), Digits(message));
    }

    // ─────────────────────────────────────────────
    // سفارش‌هایی که نباید یادآوری بگیرند
    // ─────────────────────────────────────────────
    [Fact]
    public async Task Order_without_a_payment_link_is_never_reminded()
    {
        await ConfigureAsync();
        var order = await CreateOrderAsync("AUTOTEST-REM-D", 100_000m, linkSentAt: null);

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
        Assert.Null(await PaymentReminderSentAtAsync(order.Id));
    }

    [Fact]
    public async Task Link_that_is_still_fresh_is_not_reminded_yet()
    {
        await ConfigureAsync(); // پیش‌فرض: ۲۴ ساعت
        var order = await CreateOrderAsync("AUTOTEST-REM-E", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-3));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
        Assert.Null(await PaymentReminderSentAtAsync(order.Id));
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("payment-disabled")]
    [InlineData("cancelled")]
    [InlineData("already-reminded")]
    public async Task Ineligible_orders_are_never_reminded(string reason)
    {
        await ConfigureAsync();
        var order = await CreateOrderAsync("AUTOTEST-REM-" + reason, 100_000m,
            linkSentAt: DateTime.UtcNow.AddHours(-48), mutate: o =>
            {
                switch (reason)
                {
                    case "paid":
                        o.PaymentStatus = PaymentStatus.Succeeded;
                        break;
                    case "payment-disabled":
                        o.IsPaymentEnabled = false;
                        break;
                    case "cancelled":
                        o.OrderStatus = OrderStatus.Cancelled;
                        break;
                    case "already-reminded":
                        o.PaymentReminderSentAt = DateTime.UtcNow.AddHours(-1);
                        break;
                }
            });

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));

        // در حالت «قبلاً یادآوری گرفته» همان زمان قبلی دست‌نخورده می‌ماند؛ در بقیه‌ی حالت‌ها
        // سفارش نباید هیچ علامتی بخورد (تا اگر وضعیتش درست شد، اجرای بعدی یادآوری را بفرستد).
        var remindedAt = await PaymentReminderSentAtAsync(order.Id);
        if (reason == "already-reminded") Assert.NotNull(remindedAt);
        else Assert.Null(remindedAt);
    }

    // ─────────────────────────────────────────────
    // تنظیمات و قالب
    // ─────────────────────────────────────────────
    [Fact]
    public async Task Zero_delay_is_read_as_24_hours_not_one_hour()
    {
        // مقدار صفر معنا ندارد؛ اگر «۱ ساعت» تفسیر شود، یادآوری تقریباً بلافاصله می‌رود
        await ConfigureAsync(s => s.PaymentReminderDelayHours = 0);
        var fresh = await CreateOrderAsync("AUTOTEST-REM-F", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-3));
        var old = await CreateOrderAsync("AUTOTEST-REM-G", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-25));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(fresh.OrderCode));
        Assert.Equal(1, await QueuedCountAsync(old.OrderCode));
    }

    [Fact]
    public async Task Custom_delay_hours_are_respected()
    {
        await ConfigureAsync(s => s.PaymentReminderDelayHours = 72);
        var order = await CreateOrderAsync("AUTOTEST-REM-H", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-48));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
    }

    [Fact]
    public async Task Disabled_reminder_setting_sends_nothing()
    {
        await ConfigureAsync(s => s.PaymentReminderEnabled = false);
        var order = await CreateOrderAsync("AUTOTEST-REM-I", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-48));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
        Assert.Null(await PaymentReminderSentAtAsync(order.Id));
    }

    [Fact]
    public async Task Sms_disabled_entirely_sends_nothing()
    {
        await ConfigureAsync(s => s.IsEnabled = false);
        var order = await CreateOrderAsync("AUTOTEST-REM-J", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-48));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
    }

    [Fact]
    public async Task Inactive_template_sends_nothing_and_marks_nothing()
    {
        await ConfigureAsync(templateActive: false);
        var order = await CreateOrderAsync("AUTOTEST-REM-K", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-48));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
        // علامت نخورده تا بعد از فعال‌کردن قالب، اجرای بعدی یادآوری را بفرستد
        Assert.Null(await PaymentReminderSentAtAsync(order.Id));
    }

    [Fact]
    public async Task Missing_base_url_sends_nothing_and_marks_nothing()
    {
        await ConfigureAsync(appBaseUrl: "");
        var order = await CreateOrderAsync("AUTOTEST-REM-L", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-48));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
        Assert.Null(await PaymentReminderSentAtAsync(order.Id));
    }

    [Fact]
    public async Task Quiet_hours_block_the_reminder_without_losing_it()
    {
        // بازه‌ی سکوت تمام ۲۴ ساعت را پوشش می‌دهد؛ نتیجه‌اش قطعی است و به ساعت اجرا وابسته نیست
        await ConfigureAsync(s =>
        {
            s.RespectQuietHours = true;
            s.QuietStartHour = 0;
            s.QuietEndHour = 24;
        });
        var order = await CreateOrderAsync("AUTOTEST-REM-M", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-48));

        await RunAsync();
        Assert.Equal(0, await QueuedCountAsync(order.OrderCode));
        Assert.Null(await PaymentReminderSentAtAsync(order.Id));
    }

    [Fact]
    public async Task Mixed_batch_sends_only_to_the_eligible_orders()
    {
        await ConfigureAsync();
        var eligible = await CreateOrderAsync("AUTOTEST-REM-N", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-30));
        var fresh = await CreateOrderAsync("AUTOTEST-REM-O", 100_000m, linkSentAt: DateTime.UtcNow.AddMinutes(-30));
        var noLink = await CreateOrderAsync("AUTOTEST-REM-P", 100_000m, linkSentAt: null);
        var paid = await CreateOrderAsync("AUTOTEST-REM-Q", 100_000m, linkSentAt: DateTime.UtcNow.AddHours(-30),
            mutate: o => o.PaymentStatus = PaymentStatus.Succeeded);

        await RunAsync();

        Assert.Equal(1, await QueuedCountAsync(eligible.OrderCode));
        Assert.Equal(0, await QueuedCountAsync(fresh.OrderCode));
        Assert.Equal(0, await QueuedCountAsync(noLink.OrderCode));
        Assert.Equal(0, await QueuedCountAsync(paid.OrderCode));
    }

    [Fact]
    public async Task A_clean_installation_sends_exactly_to_the_one_eligible_order()
    {
        // این آزمون روی یک میزبان تازه (دیتابیس خالی) اجرا می‌شود تا «تعداد ارسالی» قابل شمارش دقیق باشد.
        using var fresh = new AppModeWebFactory();
        await fresh.EnsureSeededAsync();

        await using (var scope = fresh.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
            db.SmsSystemSettings.Add(new SmsSystemSetting
            {
                IsEnabled = true,
                SandboxMode = true,
                RespectQuietHours = false,
                PaymentReminderEnabled = true,
                PaymentReminderDelayHours = 24
            });
            db.SmsTemplates.Add(new SmsTemplate
            {
                Scenario = SmsScenario.PaymentReminderCustomer,
                Title = "یادآوری پرداخت (آزمون)",
                BodyText = TemplateBody,
                IsActive = true
            });

            void AddOrder(string code, DateTime? linkSentAt, bool paid = false)
            {
                var order = new Order
                {
                    OrderCode = code,
                    UserId = TestAuthHandler.UserId,
                    OrderStatus = paid ? OrderStatus.Paid : OrderStatus.PendingPayment,
                    PaymentStatus = paid ? PaymentStatus.Succeeded : PaymentStatus.Unpaid,
                    IsPaymentEnabled = !paid,
                    CustomerPhone = CustomerPhone,
                    CustomerName = TestAuthHandler.FullName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                order.Items.Add(new OrderItem
                {
                    ItemType = OrderItemType.Product,
                    Quantity = 1,
                    UnitPriceSnapshot = 100_000m,
                    TotalPriceSnapshot = 100_000m
                });
                db.Orders.Add(order);
                db.SaveChanges();

                if (linkSentAt.HasValue)
                {
                    db.SmsLinkTrackings.Add(new SmsLinkTracking
                    {
                        OrderId = order.Id,
                        Kind = SmsLinkKind.WaitingPayment,
                        SentAt = linkSentAt.Value
                    });
                    db.SaveChanges();
                }
            }

            AddOrder("AUTOTEST-CLEAN-YES", DateTime.UtcNow.AddHours(-30));
            AddOrder("AUTOTEST-CLEAN-FRESH", DateTime.UtcNow.AddMinutes(-20));
            AddOrder("AUTOTEST-CLEAN-PAID", DateTime.UtcNow.AddHours(-30), paid: true);
            db.SaveChanges();
        }

        await using var runScope = fresh.Services.CreateAsyncScope();
        var sent = await runScope.ServiceProvider.GetRequiredService<SmsService>().SendPaymentRemindersAsync();

        Assert.Equal(1, sent);

        await using var checkScope = fresh.Services.CreateAsyncScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
        var messages = await checkDb.SmsOutboxItems.AsNoTracking()
            .Where(i => i.Scenario == SmsScenario.PaymentReminderCustomer)
            .Select(i => i.Message)
            .ToListAsync();

        var message = Assert.Single(messages);
        Assert.Contains("AUTOTEST-CLEAN-YES", message);
    }

    // ─────────────────────────────────────────────
    // قاعده بازه سکوت (بدون وابستگی به ساعت واقعی)
    // ─────────────────────────────────────────────
    [Fact]
    public void Quiet_hour_rule_is_deterministic()
    {
        var disabled = new SmsSystemSetting { RespectQuietHours = false, QuietStartHour = 0, QuietEndHour = 24 };
        Assert.False(SmsService.IsQuietHour(disabled));

        var wholeDay = new SmsSystemSetting { RespectQuietHours = true, QuietStartHour = 0, QuietEndHour = 24 };
        Assert.True(SmsService.IsQuietHour(wholeDay));

        var emptyWindow = new SmsSystemSetting { RespectQuietHours = true, QuietStartHour = 12, QuietEndHour = 12 };
        Assert.False(SmsService.IsQuietHour(emptyWindow));
    }

    // ─────────────────────────────────────────────
    // ابزارها
    // ─────────────────────────────────────────────
    private async Task<int> RunAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SmsService>().SendPaymentRemindersAsync();
    }

    private async Task<SmsSystemSetting> ConfigureAsync(
        Action<SmsSystemSetting>? configure = null,
        bool templateActive = true,
        string appBaseUrl = BaseUrl)
    {
        await _factory.EnsureSeededAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

        var settings = await db.SmsSystemSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SmsSystemSetting();
            db.SmsSystemSettings.Add(settings);
        }

        // مقادیر پایه‌ی قطعی آزمون (بدون ساعات سکوت، با یادآوری فعال و تأخیر ۲۴ ساعت)
        settings.IsEnabled = true;
        settings.SandboxMode = true;
        settings.RespectQuietHours = false;
        settings.PaymentReminderEnabled = true;
        settings.PaymentReminderDelayHours = 24;
        configure?.Invoke(settings);

        var template = await db.SmsTemplates.FirstOrDefaultAsync(t => t.Scenario == SmsScenario.PaymentReminderCustomer);
        if (template == null)
        {
            template = new SmsTemplate { Scenario = SmsScenario.PaymentReminderCustomer };
            db.SmsTemplates.Add(template);
        }
        template.Title = "یادآوری پرداخت (آزمون)";
        template.BodyText = TemplateBody;
        template.IsActive = templateActive;

        // هر آزمون وضعیت پایه را بازنشانی می‌کند تا نتیجه‌اش به ترتیب اجرای آزمون‌ها وابسته نباشد
        var theme = await db.ThemeSettings.FirstOrDefaultAsync();
        if (theme != null) theme.AppBaseUrl = appBaseUrl;

        await db.SaveChangesAsync();
        return settings;
    }

    private async Task<Order> CreateOrderAsync(
        string code,
        decimal amount,
        DateTime? linkSentAt,
        Action<Order>? mutate = null)
    {
        await _factory.EnsureSeededAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

        var order = new Order
        {
            OrderCode = code,
            UserId = TestAuthHandler.UserId,
            OrderStatus = OrderStatus.PendingPayment,
            PaymentStatus = PaymentStatus.Unpaid,
            IsPaymentEnabled = true,
            DeliveryFeeSnapshot = 0m,
            CustomerName = TestAuthHandler.FullName,
            CustomerPhone = CustomerPhone,
            CustomerFullAddress = "تهران، خیابان آزمون",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem
        {
            ItemType = OrderItemType.Product,
            Quantity = 1,
            UnitPriceSnapshot = amount,
            TotalPriceSnapshot = amount
        });
        mutate?.Invoke(order);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        if (linkSentAt.HasValue)
        {
            // همان ردیف اثرسنجی که پیامک «آماده پرداخت» می‌سازد: لنگر زمانی یادآوری
            db.SmsLinkTrackings.Add(new SmsLinkTracking
            {
                OrderId = order.Id,
                Kind = SmsLinkKind.WaitingPayment,
                SentAt = linkSentAt.Value
            });
            await db.SaveChangesAsync();
        }

        return order;
    }

    private async Task<DateTime?> PaymentReminderSentAtAsync(int orderId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
        return (await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId)).PaymentReminderSentAt;
    }

    private async Task<int> QueuedCountAsync(string orderCode)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
        return await db.SmsOutboxItems.CountAsync(i =>
            i.Scenario == SmsScenario.PaymentReminderCustomer && i.Message.Contains(orderCode));
    }

    private async Task<string> SingleQueuedMessageAsync(string orderCode)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
        var messages = await db.SmsOutboxItems.AsNoTracking()
            .Where(i => i.Scenario == SmsScenario.PaymentReminderCustomer && i.Message.Contains(orderCode))
            .Select(i => i.Message)
            .ToListAsync();

        Assert.Single(messages);
        return messages[0];
    }

    /// <summary>
    /// فقط ارقام متن را برمی‌گرداند (با تبدیل ارقام فارسی/عربی به لاتین و حذف جداکننده‌های
    /// هزارگان)، تا مقایسه‌ی مبلغ در متن پیامک مستقل از قالب‌بندی باشد.
    /// </summary>
    private static string Digits(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch >= '\u06F0' && ch <= '\u06F9') builder.Append((char)('0' + (ch - '\u06F0')));
            else if (ch >= '\u0660' && ch <= '\u0669') builder.Append((char)('0' + (ch - '\u0660')));
            else if (ch >= '0' && ch <= '9') builder.Append(ch);
        }

        return builder.ToString();
    }
}
