using System.Net;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// 🧪 آزمون جداسازی «اپلیکیشن» و «وبسایت».
///
/// قاعده‌ی محصول: حالت صفحه فقط از User-Agent (پوسته‌ی اندرویدی <c>SugarShopApp/</c>) تعیین می‌شود و
/// همان یک مقدار هم روی <c>&lt;html data-app-mode&gt;</c> می‌نشیند و هم رندر عناصر را تعیین می‌کند.
/// هیچ صفحه‌ای نباید بیرون از حالت خودش عنصر رندر کند: درخواست اپ هیچ عنصر وب‌محور
/// (<c>data-ui-scope="web"</c>) و درخواست مرورگر هیچ عنصر اپ‌محور (<c>data-ui-scope="app"</c>) نمی‌بیند.
///
/// این آزمون روی فهرست صفحه‌های <see cref="TestPageCatalog"/> در هر دو حالت اجرا می‌شود، پس اگر
/// کسی بعداً بخشی را شرطی نکرد یا حالت را از کوکی/حافظه گرفت، همین‌جا شکست می‌خورد.
/// </summary>
public class AppModeSeparationTests : IClassFixture<AppModeWebFactory>
{
    private const string BottomNav = "//nav[contains(@class,'pwa-bottom-nav')]";

    private readonly AppModeWebFactory _factory;

    public AppModeSeparationTests(AppModeWebFactory factory) => _factory = factory;

    public static IEnumerable<object[]> AppPages =>
        TestPageCatalog.Pages.Select(p => new object[] { p.Name, p.Url });

    public static IEnumerable<object[]> SitePages =>
        TestPageCatalog.SiteShellPages.Select(p => new object[] { p.Name, p.Url });

    public static IEnumerable<object[]> NavPages =>
        TestPageCatalog.PagesWithBottomNav.Select(p => new object[] { p.Name, p.Url });

    public static IEnumerable<object[]> BackofficePages =>
        TestPageCatalog.BackofficePages.Select(p => new object[] { p.Name, p.Url });

    // ─────────────────────────────────────────────────────────────
    // ۱) درخواست از داخل اپ → هیچ عنصر وب‌محور
    // ─────────────────────────────────────────────────────────────
    [Theory]
    [MemberData(nameof(AppPages))]
    public async Task AppRequest_renders_no_web_only_element(string name, string url)
    {
        var (response, html) = await GetAsync(url, AppModeWebFactory.AppUserAgent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(html);

        var doc = HtmlProbe.Parse(html!);

        Assert.True(HtmlProbe.AppMode(doc) == "inapp",
            $"«{name}» ({url}): درخواست اپ باید data-app-mode=\"inapp\" باشد، ولی " +
            $"\"{HtmlProbe.AppMode(doc)}\" است.");

        var webOnly = HtmlProbe.WithScope(doc, "web");
        Assert.True(webOnly.Count == 0,
            $"«{name}» ({url}): داخل اپ نباید هیچ عنصر وب‌محوری رندر شود، ولی " +
            $"{webOnly.Count} عنصر با data-ui-scope=\"web\" پیدا شد → {HtmlProbe.Describe(webOnly)}");
    }

    // ─────────────────────────────────────────────────────────────
    // ۲) درخواست از مرورگر → هیچ عنصر اپ‌محور
    // ─────────────────────────────────────────────────────────────
    [Theory]
    [MemberData(nameof(AppPages))]
    public async Task BrowserRequest_renders_no_app_only_element(string name, string url)
    {
        var (response, html) = await GetAsync(url, AppModeWebFactory.BrowserUserAgent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(html);

        var doc = HtmlProbe.Parse(html!);

        Assert.True(HtmlProbe.AppMode(doc) == "browser",
            $"«{name}» ({url}): درخواست مرورگر باید data-app-mode=\"browser\" باشد، ولی " +
            $"\"{HtmlProbe.AppMode(doc)}\" است.");

        var appOnly = HtmlProbe.WithScope(doc, "app");
        Assert.True(appOnly.Count == 0,
            $"«{name}» ({url}): در وب نباید هیچ عنصر اپ‌محوری رندر شود، ولی " +
            $"{appOnly.Count} عنصر با data-ui-scope=\"app\" پیدا شد → {HtmlProbe.Describe(appOnly)}");
    }

    // ─────────────────────────────────────────────────────────────
    // ۳) عنصری که باید فقط در یک حالت باشد، واقعاً همان‌جاست
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Home_shows_app_blocks_only_inside_the_app()
    {
        var app = HtmlProbe.Parse((await GetAsync("/", AppModeWebFactory.AppUserAgent)).Html!);
        var web = HtmlProbe.Parse((await GetAsync("/", AppModeWebFactory.BrowserUserAgent)).Html!);

        Assert.True(HtmlProbe.Exists(app, "//*[contains(@class,'app-home')]"),
            "صفحه اصلی در حالت اپ باید بلوک اختصاصی اپ (سربرگ/میان‌بر/سفارش‌ها) را رندر کند.");
        Assert.False(HtmlProbe.Exists(web, "//*[contains(@class,'app-home')]"),
            "صفحه اصلی در وب نباید بلوک اپ‌محور (app-home) را رندر کند.");

        Assert.True(HtmlProbe.Exists(web, "//*[contains(@class,'category-grid')]"),
            "صفحه اصلی در وب باید ویترین دسته‌بندی‌ها را داشته باشد.");
        Assert.False(HtmlProbe.Exists(app, "//*[contains(@class,'category-grid')]"),
            "صفحه اصلی در اپ نباید ویترین تکراری دسته‌بندی‌ها را رندر کند.");
    }

    [Fact]
    public async Task Install_banner_and_app_promo_are_web_only()
    {
        var web = HtmlProbe.Parse((await GetAsync("/", AppModeWebFactory.BrowserUserAgent)).Html!);
        var app = HtmlProbe.Parse((await GetAsync("/", AppModeWebFactory.AppUserAgent)).Html!);

        Assert.True(HtmlProbe.Exists(web, "//*[@id='pwaInstallBanner']"),
            "بنر «نصب اپلیکیشن» باید در وب دیده شود.");
        Assert.False(HtmlProbe.Exists(app, "//*[@id='pwaInstallBanner']"),
            "داخل اپ، بنر «نصب اپلیکیشن» نباید رندر شود.");
        Assert.False(HtmlProbe.Exists(app, "//a[contains(@href,'/App/Download')]"),
            "داخل اپ نباید هیچ لینکی به «دانلود اپلیکیشن» باشد.");
        Assert.True(HtmlProbe.Exists(web, "//a[contains(@href,'/App/Download')]"),
            "در وب باید راهی برای دانلود اپلیکیشن وجود داشته باشد.");
    }

    [Theory]
    [MemberData(nameof(NavPages))]
    public async Task Bottom_nav_items_change_with_the_mode(string name, string url)
    {
        var app = HtmlProbe.Parse((await GetAsync(url, AppModeWebFactory.AppUserAgent)).Html!);
        var web = HtmlProbe.Parse((await GetAsync(url, AppModeWebFactory.BrowserUserAgent)).Html!);

        // آیتم‌های وب‌محور نوار پایین: هرگز داخل اپ
        Assert.False(HtmlProbe.Exists(app, $"{BottomNav}//a[@href='/Category/All']"),
            $"«{name}»: نوار پایین اپ نباید آیتم وب‌محور «دسته‌بندی‌ها» را داشته باشد.");
        Assert.False(HtmlProbe.Exists(app, $"{BottomNav}//a[contains(@href,'/CustomCakeOrder/Create')]"),
            $"«{name}»: نوار پایین اپ نباید آیتم وب‌محور «کیک سفارشی» را داشته باشد.");

        // آیتم اپ‌محور «سفارش‌ها» فقط داخل اپ
        Assert.True(HtmlProbe.Exists(app, $"{BottomNav}//a[contains(@href,'/Profile/Orders')]"),
            $"«{name}»: نوار پایین اپ باید آیتم «سفارش‌ها» را داشته باشد.");
        Assert.False(HtmlProbe.Exists(web, $"{BottomNav}//a[contains(@href,'/Profile/Orders')]"),
            $"«{name}»: نوار پایین وب نباید آیتم اپ‌محور «سفارش‌ها» را داشته باشد.");

        // ناوبری وب در هر دو حالت باید «سبد» و «پروفایل/ورود» را داشته باشد
        Assert.True(HtmlProbe.Exists(app, $"{BottomNav}//a[@href='/Cart']"), $"«{name}»: نوار پایین اپ فاقد «سبد» است.");
        Assert.True(HtmlProbe.Exists(web, $"{BottomNav}//a[@href='/Cart']"), $"«{name}»: نوار پایین وب فاقد «سبد» است.");
    }

    [Fact]
    public async Task Public_statement_document_respects_the_mode()
    {
        // صفحه‌ی سند صورت‌حساب (لینک پیامکی) چیدمان سایت را ندارد؛ حالتش را هم سمت سرور
        // از User-Agent می‌گیرد و بخش‌های وب‌محور (دعوت به ورود) و اپ‌محور (پیام «داخل اپ هستید»)
        // فقط در حالت خودشان رندر می‌شوند.
        await _factory.EnsureSeededAsync();
        Assert.False(string.IsNullOrWhiteSpace(_factory.StatementToken), "توکن صورت‌حساب آزمون ساخته نشد.");
        var url = "/s/" + _factory.StatementToken;

        var (appResponse, appHtml) = await GetAsync(url, AppModeWebFactory.AppUserAgent);
        var (webResponse, webHtml) = await GetAsync(url, AppModeWebFactory.BrowserUserAgent);
        Assert.Equal(HttpStatusCode.OK, appResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, webResponse.StatusCode);

        var app = HtmlProbe.Parse(appHtml!);
        var web = HtmlProbe.Parse(webHtml!);

        Assert.Equal("inapp", HtmlProbe.AppMode(app));
        Assert.Equal("browser", HtmlProbe.AppMode(web));
        Assert.Empty(HtmlProbe.WithScope(app, "web"));
        Assert.Empty(HtmlProbe.WithScope(web, "app"));
        Assert.True(HtmlProbe.Exists(app, "//*[@data-ui-scope='app']"),
            "داخل اپ، سند باید پیام مخصوص اپ (بازگشت به سفارش) را داشته باشد.");
        Assert.True(HtmlProbe.Exists(web, "//*[@data-ui-scope='web']"),
            "در مرورگر، سند باید دعوت به ورود برای دیدن همیشگی سفارش‌ها را داشته باشد.");
    }

    [Theory]
    [MemberData(nameof(BackofficePages))]
    public async Task Backoffice_gets_the_app_nav_only_inside_the_app(string name, string url)
    {
        // در وب، پنل مدیریت و سرآشپز ناوبری اختصاصی خودشان را دارند و نوار پایین سایت
        // روی آن‌ها سوار نمی‌شود؛ فقط داخل اپ کاربر به خانه/سبد/سفارش‌ها/پروفایل دسترسی می‌خواهد.
        var app = HtmlProbe.Parse((await GetAsync(url, AppModeWebFactory.AppUserAgent)).Html!);
        var web = HtmlProbe.Parse((await GetAsync(url, AppModeWebFactory.BrowserUserAgent)).Html!);

        Assert.False(HtmlProbe.Exists(web, BottomNav),
            $"«{name}» ({url}): در وب نباید نوار پایین اپ روی پنل سوار شود.");

        Assert.True(HtmlProbe.Exists(app, BottomNav),
            $"«{name}» ({url}): داخل اپ کاربر باید از نوار پایین به بخش‌های اصلی دسترسی داشته باشد.");
        Assert.True(HtmlProbe.Exists(app, $"{BottomNav}//a[contains(@href,'/Profile/Orders')]"),
            $"«{name}» ({url}): نوار پایین اپ باید آیتم «سفارش‌ها» را داشته باشد.");
    }

    [Theory]
    [MemberData(nameof(SitePages))]
    public async Task Fixed_header_class_is_web_only(string name, string url)
    {
        // هدر Fixed روی جریان صفحه نیست و فقط در وب به کلاس جبران‌کننده header-fixed نیاز دارد؛
        // داخل اپ هدر نوار باریک sticky است و این کلاس نباید بیاید (وگرنه فاصله دو بار حساب می‌شود).
        var web = HtmlProbe.Parse((await GetAsync(url, AppModeWebFactory.BrowserUserAgent)).Html!);
        var app = HtmlProbe.Parse((await GetAsync(url, AppModeWebFactory.AppUserAgent)).Html!);

        Assert.True(HtmlProbe.RootClasses(web).Contains("header-fixed"),
            $"«{name}» ({url}): با هدر Fixed، صفحه‌ی وب باید کلاس header-fixed داشته باشد.");
        Assert.False(HtmlProbe.RootClasses(app).Contains("header-fixed"),
            $"«{name}» ({url}): داخل اپ نباید کلاس header-fixed وجود داشته باشد.");
    }

    private async Task<(HttpResponseMessage Response, string? Html)> GetAsync(string url, string userAgent)
    {
        using var client = await _factory.CreatePageClientAsync(userAgent);
        var response = await client.GetAsync(url);
        var html = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
        return (response, html);
    }
}
