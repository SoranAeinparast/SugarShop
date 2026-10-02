using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// میزبان آزمونِ «لیست سفارش‌های پنل مدیریت» با داده‌ی کاملاً قطعی.
///
/// دیتابیس in-memory این کلاس مستقل است، پس اول همه‌ی سفارش‌ها پاک می‌شوند و سپس دقیقاً
/// ۴۳ سفارش فروشگاهی (<c>ADM-0001</c>..<c>ADM-0043</c>) به‌همراه دو سفارش «داخلی» ساخته می‌شود.
/// زمان ساخت صعودی است (پس <c>ADM-0043</c> جدیدترین ردیف لیست است) و سفارش‌های داخلی عمداً
/// از همه جدیدترند تا اگر فیلترشان کار نکند، اولِ لیست دیده شوند.
/// </summary>
public sealed class AdminOrderListFactory : AppModeWebFactory
{
    public const int ShopOrderCount = 43;
    public const int PageSize = 20;

    /// <summary>مبلغ هر سفارش فروشگاهی: (شماره × ۱۰۰٬۰۰۰) + ۵۰٬۰۰۰ تومان هزینه پیک.</summary>
    private const decimal UnitAmount = 100_000m;
    private const decimal DeliveryFee = 50_000m;

    public static decimal AmountOf(int index) => index * UnitAmount + DeliveryFee;

    /// <summary>وضعیت هر سفارش بر اساس شماره‌اش (چرخه‌ی پنج‌تایی).</summary>
    public static OrderStatus StatusOf(int index) => (index % 5) switch
    {
        0 => OrderStatus.Delivered,
        1 => OrderStatus.PendingPayment,
        2 => OrderStatus.Preparing,
        3 => OrderStatus.Shipped,
        _ => OrderStatus.Cancelled
    };

    private bool _seeded;

    public async Task SeedAsync()
    {
        if (_seeded) return;
        await EnsureSeededAsync(); // کاربر و تنظیمات پایه‌ی زیرساخت آزمون

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

        db.Orders.RemoveRange(db.Orders.ToList());
        await db.SaveChangesAsync();

        var anchor = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        for (int i = 1; i <= ShopOrderCount; i++)
        {
            db.Orders.Add(new Order
            {
                OrderCode = $"ADM-{i:0000}",
                UserId = TestAuthHandler.UserId,
                OrderStatus = StatusOf(i),
                PaymentStatus = PaymentStatus.Succeeded,
                TotalAmountSnapshot = i * UnitAmount,
                DeliveryFeeSnapshot = DeliveryFee,
                FinalTotalAmount = AmountOf(i),
                FinalTotalWeightGrams = i * 10,
                CustomerName = $"مشتری شماره {i:0000}",
                CustomerPhone = $"0912000{i:0000}",
                CreatedAt = anchor.AddMinutes(i),
                UpdatedAt = anchor.AddMinutes(i)
            });
        }

        db.Orders.Add(InternalOrder("INTERNAL-WALLET-1", OrderNotes.WalletRecharge, anchor.AddYears(1)));
        db.Orders.Add(InternalOrder("INTERNAL-CAKE-1", OrderNotes.ForCustomCakeOrder(7), anchor.AddYears(1).AddMinutes(1)));

        await db.SaveChangesAsync();
        _seeded = true;
    }

    private static Order InternalOrder(string code, string notes, DateTime createdAt) => new()
    {
        OrderCode = code,
        OrderStatus = OrderStatus.Paid,
        PaymentStatus = PaymentStatus.Succeeded,
        Notes = notes,
        TotalAmountSnapshot = 1m,
        FinalTotalAmount = 1m,
        CustomerName = "سفارش داخلی",
        CustomerPhone = "09120000000",
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };
}

/// <summary>
/// 🧪 قفل‌کردن «صفحه‌بندی و جست‌وجوی سمت سرور» در لیست سفارش‌های پنل مدیریت.
///
/// قاعده‌ی محصول: با هر تعداد سفارش، هر بار دیدن این صفحه باید فقط **یک صفحه** از دیتابیس خوانده
/// شود و همه‌ی فیلترها، شمارنده‌ها و جمع‌ها در SQL حساب شوند. پس اگر کسی بعداً دوباره همه‌ی
/// سفارش‌ها را لود کند و در حافظه/ویو صفحه‌بندی کند، این آزمون‌ها می‌شکنند:
///
///   • ردیف‌های HTML فقط همان صفحه‌اند (کد سفارش‌های صفحه‌های دیگر در HTML نیستند)،
///   • شمارنده‌ی تب‌ها و جمع مبلغ مربوط به «کل مجموعه‌ی فیلترشده» است، نه صفحه‌ی جاری،
///   • تب‌ها (وضعیت) و جست‌وجو سمت سرور اعمال می‌شوند و پارامتر ناشناخته به دیتابیس نمی‌رسد،
///   • سفارش‌های داخلی (شارژ کیف پول و سفارش موقت کیک) هرگز در لیست و آمار دیده نمی‌شوند،
///   • خروجی اکسل هم دقیقاً همان فیلترهای لیست را می‌گیرد.
/// </summary>
public class AdminOrdersListTests : IClassFixture<AdminOrderListFactory>, IAsyncLifetime
{
    private readonly AdminOrderListFactory _factory;

    public AdminOrdersListTests(AdminOrderListFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.SeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ─────────────────────────────────────────────────────────────
    // ۱) صفحه‌بندی
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task First_page_contains_only_the_newest_page_of_orders()
    {
        var doc = await GetAsync("/Admin/Orders");

        var codes = DataRowCodes(doc);
        Assert.Equal(AdminOrderListFactory.PageSize, codes.Count);
        Assert.Equal("ADM-0043", codes[0]);
        Assert.Equal("ADM-0024", codes[^1]);

        // شمارش‌ها و جمع‌های پانویس مربوط به «کل مجموعه» است، نه همین ۲۰ ردیف
        Assert.Equal(AdminOrderListFactory.ShopOrderCount, TotalOrders(doc));
        Assert.Equal(96_750_000m, TotalPayments(doc));
        Assert.Equal((1, 20, 43), ShownRange(doc));

        // ⬅ مهم‌ترین قفل: کد سفارش صفحه‌های دیگر نباید اصلاً در HTML صفحه باشد
        Assert.DoesNotContain("ADM-0001", doc.DocumentNode.InnerHtml);
    }

    [Fact]
    public async Task Middle_page_shows_exactly_its_own_window()
    {
        var doc = await GetAsync("/Admin/Orders?page=2");

        var codes = DataRowCodes(doc);
        Assert.Equal(AdminOrderListFactory.PageSize, codes.Count);
        Assert.Equal("ADM-0023", codes[0]);
        Assert.Equal("ADM-0004", codes[^1]);
        Assert.Equal((21, 40, 43), ShownRange(doc));
        Assert.DoesNotContain("ADM-0043", doc.DocumentNode.InnerHtml);
    }

    [Theory]
    [InlineData("3", "ADM-0003", 3)]        // آخرین صفحه
    [InlineData("9999", "ADM-0003", 3)]     // بالاتر از آخرین صفحه → آخرین صفحه
    [InlineData("0", "ADM-0043", 20)]        // صفر → صفحه اول
    [InlineData("-7", "ADM-0043", 20)]       // منفی → صفحه اول
    public async Task Out_of_range_page_numbers_are_clamped(string page, string expectedFirstCode, int expectedRows)
    {
        var doc = await GetAsync($"/Admin/Orders?page={page}");

        Assert.Equal(expectedFirstCode, DataRowCodes(doc)[0]);
        Assert.Equal(expectedRows, DataRowCodes(doc).Count);
        Assert.Equal(AdminOrderListFactory.ShopOrderCount, TotalOrders(doc));
    }

    [Fact]
    public async Task Pagination_and_tab_links_keep_the_current_search()
    {
        // جست‌وجوی «ADM-00» هر ۴۳ سفارش را پیدا می‌کند → سه صفحه
        var paged = await GetAsync("/Admin/Orders?search=ADM-00&page=1");
        var nextLink = paged.DocumentNode.SelectSingleNode("//ul[contains(@class,'pagination')]//a[contains(@href,'page=2')]");
        Assert.NotNull(nextLink);
        Assert.Contains("search=ADM-00", Href(nextLink!));

        // لینک تب‌ها هم عبارت جست‌وجو را از دست نمی‌دهد
        var filtered = await GetAsync("/Admin/Orders?status=delivered&search=ADM-00");
        var deliveredTab = filtered.DocumentNode
            .SelectSingleNode("//ul[contains(@class,'nav-tabs')]//a[contains(@href,'status=delivered')]");
        Assert.NotNull(deliveredTab);
        Assert.Contains("search=ADM-00", Href(deliveredTab!));
    }

    // ─────────────────────────────────────────────────────────────
    // ۲) شمارنده‌ی تب‌ها = کل مجموعه، نه صفحه‌ی جاری
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Tab_counters_count_the_whole_filtered_set()
    {
        // چرخه‌ی «i % 5» روی ۱..۴۳: باقی‌مانده‌های ۱، ۲ و ۳ نه‌تا و باقی‌مانده‌های ۰ و ۴ هشت‌تا
        var doc = await GetAsync("/Admin/Orders");
        Assert.Equal(43, Badge(doc, "all"));
        Assert.Equal(9, Badge(doc, "pending"));
        Assert.Equal(9, Badge(doc, "preparing"));
        Assert.Equal(9, Badge(doc, "shipped"));
        Assert.Equal(8, Badge(doc, "delivered"));
        Assert.Equal(8, Badge(doc, "cancelled"));

        // انتخاب یک تب، شمارنده‌ی بقیه را کوچک نمی‌کند (یعنی شمارنده با صفحه‌ی جاری حساب نشده)
        var delivered = await GetAsync("/Admin/Orders?status=delivered");
        Assert.Equal(43, Badge(delivered, "all"));
        Assert.Equal(9, Badge(delivered, "pending"));
        Assert.Equal(8, Badge(delivered, "delivered"));
    }

    [Fact]
    public async Task Tab_counters_follow_the_search_term_not_the_page()
    {
        // «ADM-00» هر ۴۳ سفارش را دارد ولی صفحه فقط ۲۰ ردیف است؛ پس شمارنده باید ۴۳ بماند
        var wide = await GetAsync("/Admin/Orders?search=ADM-00");
        Assert.Equal(20, DataRowCodes(wide).Count);
        Assert.Equal(43, Badge(wide, "all"));
        Assert.Equal(43, TotalOrders(wide));

        // عبارت باریک‌تر: فقط ADM-0001..ADM-0009
        var narrow = await GetAsync("/Admin/Orders?search=ADM-000");
        Assert.Equal(9, DataRowCodes(narrow).Count);
        Assert.Equal(9, Badge(narrow, "all"));
        Assert.Equal(2, Badge(narrow, "pending"));
        Assert.Equal(1, Badge(narrow, "delivered"));
    }

    // ─────────────────────────────────────────────────────────────
    // ۳) فیلتر وضعیت سمت سرور (شامل تب‌های «ارسال شده» و «لغو شده»)
    // ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("pending", "در انتظار پرداخت", 9, 19_350_000)]
    [InlineData("preparing", "در حال آماده‌سازی", 9, 20_250_000)]
    [InlineData("shipped", "ارسال شده", 9, 21_150_000)]
    [InlineData("delivered", "تحویل شده", 8, 18_400_000)]
    [InlineData("cancelled", "لغو شده", 8, 17_600_000)]
    public async Task Status_tab_filters_on_the_server(string status, string statusLabel, int expectedCount, int expectedSum)
    {
        var doc = await GetAsync($"/Admin/Orders?status={status}");

        Assert.Equal(expectedCount, DataRowCodes(doc).Count);
        Assert.Equal(expectedCount, TotalOrders(doc));
        Assert.Equal(expectedCount, Badge(doc, status));

        // جمع مبلغ فقط همان وضعیت را می‌شمارد (نه کل سفارش‌ها)
        Assert.Equal(expectedSum, TotalPayments(doc));

        // هر ردیفِ صفحه واقعاً همان وضعیت است
        Assert.All(DataRowStatuses(doc), text => Assert.Equal(statusLabel, text));

        // تب فعال همان تب انتخاب‌شده است
        var activeTab = doc.DocumentNode.SelectSingleNode("//ul[contains(@class,'nav-tabs')]//a[contains(@class,'active')]");
        Assert.NotNull(activeTab);
        Assert.Contains($"status={status}", Href(activeTab!));
    }

    [Theory]
    [InlineData("hacked') OR 1=1 --")]
    [InlineData("")]
    [InlineData("مقدار-ناشناخته")]
    public async Task Unknown_status_parameter_falls_back_to_all_orders(string status)
    {
        var doc = await GetAsync("/Admin/Orders?status=" + Uri.EscapeDataString(status));

        Assert.Equal(AdminOrderListFactory.ShopOrderCount, TotalOrders(doc));
        Assert.Equal(AdminOrderListFactory.PageSize, DataRowCodes(doc).Count);
        Assert.Equal(96_750_000m, TotalPayments(doc));
    }

    // ─────────────────────────────────────────────────────────────
    // ۴) جست‌وجوی سمت سرور
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Search_by_order_code_returns_exactly_that_order()
    {
        var doc = await GetAsync("/Admin/Orders?search=ADM-0021");

        Assert.Equal(new[] { "ADM-0021" }, DataRowCodes(doc));
        Assert.Equal(1, TotalOrders(doc));
        Assert.Equal(2_150_000m, TotalPayments(doc));
        Assert.Equal((1, 1, 1), ShownRange(doc));
    }

    [Fact]
    public async Task Search_accepts_persian_digits()
    {
        // کاربر با صفحه‌کلید فارسی «۰۰۲۱» تایپ می‌کند؛ باید همان ADM-0021 پیدا شود
        var doc = await GetAsync("/Admin/Orders?search=" + Uri.EscapeDataString("ADM-۰۰۲۱"));

        Assert.Equal(new[] { "ADM-0021" }, DataRowCodes(doc));
    }

    [Fact]
    public async Task Search_matches_recipient_name_and_phone()
    {
        var byName = await GetAsync("/Admin/Orders?search=" + Uri.EscapeDataString("شماره 0007"));
        Assert.Equal(new[] { "ADM-0007" }, DataRowCodes(byName));

        var byPhone = await GetAsync("/Admin/Orders?search=09120000007");
        Assert.Equal(new[] { "ADM-0007" }, DataRowCodes(byPhone));
    }

    [Fact]
    public async Task Search_without_a_hit_shows_the_empty_state()
    {
        var doc = await GetAsync("/Admin/Orders?search=" + Uri.EscapeDataString("سفارش-ناموجود-۱۲۳"));

        Assert.Empty(DataRowCodes(doc));
        Assert.Equal(0, TotalOrders(doc));
        Assert.Contains("یافت نشد", doc.DocumentNode.InnerText);
        // تب‌ها هم صفر می‌شوند، یعنی شمارنده‌ها روی همان جست‌وجو حساب شده‌اند
        Assert.Equal(0, Badge(doc, "all"));
    }

    [Fact]
    public async Task Search_paginates_the_filtered_set()
    {
        var page2 = await GetAsync("/Admin/Orders?search=ADM-00&page=2");

        Assert.Equal(AdminOrderListFactory.PageSize, DataRowCodes(page2).Count);
        Assert.Equal("ADM-0023", DataRowCodes(page2)[0]);
        Assert.Equal(43, TotalOrders(page2));
    }

    // ─────────────────────────────────────────────────────────────
    // ۵) سفارش‌های داخلی هرگز در لیست پنل نیستند
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Internal_orders_are_excluded_from_list_counters_and_search()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
            // در دیتابیس ۴۵ سفارش هست (۴۳ فروشگاهی + ۲ داخلی) …
            Assert.Equal(AdminOrderListFactory.ShopOrderCount + 2, await db.Orders.CountAsync());
        }

        // … ولی لیست پنل و همه‌ی آمارش فقط ۴۳ سفارش فروشگاهی را می‌بیند
        var doc = await GetAsync("/Admin/Orders");
        Assert.Equal(AdminOrderListFactory.ShopOrderCount, TotalOrders(doc));
        Assert.Equal("ADM-0043", DataRowCodes(doc)[0]);
        Assert.DoesNotContain("INTERNAL-", doc.DocumentNode.InnerHtml);

        var byInternalCode = await GetAsync("/Admin/Orders?search=INTERNAL");
        Assert.Empty(DataRowCodes(byInternalCode));
        Assert.Equal(0, TotalOrders(byInternalCode));
    }

    [Fact]
    public async Task Marker_column_matches_exactly_the_internal_orders()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

        // فقط دو سفارش داخلی نشان‌دار هستند…
        var markedCodes = await db.Orders.AsNoTracking()
            .Where(o => o.IsInternal)
            .Select(o => o.OrderCode)
            .ToListAsync();
        Assert.Equal(
            new[] { "INTERNAL-CAKE-1", "INTERNAL-WALLET-1" },
            markedCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray());

        // …و بقیه بدون نشان‌اند، یعنی لیست پنل با فیلتر نشانگر دقیقاً همان ۴۳ سفارش را می‌بیند
        Assert.Equal(AdminOrderListFactory.ShopOrderCount, await db.Orders.CountAsync(o => !o.IsInternal));
    }

    // ─────────────────────────────────────────────────────────────
    // ۶) خروجی اکسل همان فیلترهای لیست را می‌گیرد
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Excel_export_uses_the_same_server_side_filters()
    {
        using var client = await _factory.CreatePageClientAsync(AppModeWebFactory.BrowserUserAgent);
        var response = await client.GetAsync("/Admin/ExportOrdersToExcel?status=delivered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal("کد سفارش", sheet.Cell(1, 1).GetString());

        // ردیف ۱ سرستون، ردیف‌های ۲..۹ هشت سفارش «تحویل شده»، سپس یک ردیف خالی و دو ردیف جمع‌بندی
        var exportedCodes = Enumerable.Range(2, 8).Select(r => sheet.Cell(r, 1).GetString()).ToList();
        Assert.All(exportedCodes, code => Assert.StartsWith("ADM-", code));
        Assert.All(Enumerable.Range(2, 8), r => Assert.Equal("تحویل شده", sheet.Cell(r, 6).GetString()));

        Assert.Equal("تعداد سفارشات", sheet.Cell(11, 1).GetString());
        Assert.Equal(8, sheet.Cell(11, 2).GetValue<int>());
        Assert.Equal(18_400_000d, sheet.Cell(12, 2).GetValue<double>(), 0);
    }

    // ─────────────────────────────────────────────────────────────
    // ابزارها
    // ─────────────────────────────────────────────────────────────
    private async Task<HtmlDocument> GetAsync(string url)
    {
        using var client = await _factory.CreatePageClientAsync(AppModeWebFactory.BrowserUserAgent);
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return HtmlProbe.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// متن یک گره، رمزگشایی‌شده.
    ///
    /// نکته‌ی مهم: متن‌های ثابت داخل ویوها عیناً نوشته می‌شوند ولی مقادیر داینامیک (مثل ارقام
    /// فارسی و نام وضعیت) توسط Razor به‌صورت موجودیت عددی (`&#x6F4;`) انکود می‌شوند؛ پس مقایسه و
    /// تجزیه‌ی عدد بدون این رمزگشایی کار نمی‌کند.
    /// </summary>
    private static string Text(HtmlNode? node) => WebUtility.HtmlDecode(node?.InnerText.Trim() ?? string.Empty);

    /// <summary>کد سفارش ردیف‌های داده (ردیف «حالت خالی» یک <c>td</c> دارد و اینجا حساب نمی‌شود).</summary>
    private static IReadOnlyList<string> DataRowCodes(HtmlDocument doc)
        => doc.DocumentNode.SelectNodes("//table//tbody/tr[count(td)=8]/td[1]")
               ?.Select(Text).ToList()
           ?? new List<string>();

    private static IReadOnlyList<string> DataRowStatuses(HtmlDocument doc)
        => doc.DocumentNode.SelectNodes("//table//tbody/tr[count(td)=8]/td[6]")
               ?.Select(Text).ToList()
           ?? new List<string>();

    private static int Badge(HtmlDocument doc, string statusKey)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//ul[contains(@class,'nav-tabs')]//a[contains(@href,'status={statusKey}')]//span[contains(@class,'badge')]");
        Assert.NotNull(node);
        return ParseNumber(Text(node));
    }

    private static int TotalOrders(HtmlDocument doc)
        => ParseNumber(MatchedValue(OrderCountRegex, FooterText(doc), "تعداد سفارشات"));

    private static decimal TotalPayments(HtmlDocument doc)
        => ParseNumber(MatchedValue(PaymentSumRegex, FooterText(doc), "جمع کل پرداخت‌ها"));

    private static (int First, int Last, int Total) ShownRange(HtmlDocument doc)
    {
        var match = RangeRegex.Match(WebUtility.HtmlDecode(doc.DocumentNode.InnerText));
        Assert.True(match.Success, "خط «نمایش ... تا ... از ... سفارش» در صفحه پیدا نشد.");
        return (ParseNumber(match.Groups[1].Value), ParseNumber(match.Groups[2].Value), ParseNumber(match.Groups[3].Value));
    }

    private static string FooterText(HtmlDocument doc)
        => Text(doc.DocumentNode.SelectSingleNode("//table//tfoot"));

    private static string Href(HtmlNode node) => WebUtility.HtmlDecode(node.GetAttributeValue("href", string.Empty));

    private static string MatchedValue(Regex regex, string text, string label)
    {
        var match = regex.Match(text);
        Assert.True(match.Success, $"مقدار «{label}» در پانویس جدول پیدا نشد. متن پانویس: {text}");
        return match.Groups[1].Value;
    }

    /// <summary>عدد فارسی/عربی با جداکننده‌ی هزارگان را به عدد لاتین تبدیل می‌کند.</summary>
    private static int ParseNumber(string text)
    {
        var digits = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch >= '0' && ch <= '9') digits.Append(ch);
            else if (ch >= '\u06F0' && ch <= '\u06F9') digits.Append((char)('0' + (ch - '\u06F0')));
            else if (ch >= '\u0660' && ch <= '\u0669') digits.Append((char)('0' + (ch - '\u0660')));
        }

        Assert.True(digits.Length > 0, $"در متن «{text}» هیچ رقمی پیدا نشد.");
        return int.Parse(digits.ToString());
    }

    // «ها» با نیم‌فاصله نوشته می‌شود؛ برای همین در الگو به‌جای همان نویسه از «.» استفاده شده
    // تا به شکل دقیق کاراکتر حساس نباشد.
    private static readonly Regex OrderCountRegex =
        new(@"تعداد سفارشات:\s*([\d۰-۹,٬]+)", RegexOptions.Singleline);

    private static readonly Regex PaymentSumRegex =
        new(@"جمع کل پرداخت.ها:\s*([\d۰-۹,٬]+)", RegexOptions.Singleline);

    private static readonly Regex RangeRegex =
        new(@"نمایش\s+([\d۰-۹,٬]+)\s+تا\s+([\d۰-۹,٬]+)\s+از\s+([\d۰-۹,٬]+)\s+سفارش", RegexOptions.Singleline);
}
