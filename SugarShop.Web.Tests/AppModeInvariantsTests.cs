using System.Net;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// ثابت‌هایی که نباید هیچ‌وقت بشکنند: حالت صفحه «مخصوص همین درخواست» است و هیچ‌جای دیگری
/// (کوکی، حافظه، کش) نگه داشته نمی‌شود. این‌ها همان چیزهایی بودند که قبلاً باعث می‌شد کاربر
/// بعد از چاپ/دانلود یا برگشت از درگاه، ناگهان چیدمان اپ را ببیند.
/// </summary>
public class AppModeInvariantsTests : IClassFixture<AppModeWebFactory>
{
    private readonly AppModeWebFactory _factory;

    public AppModeInvariantsTests(AppModeWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Stale_appmode_cookie_does_not_flip_the_browser_into_app_mode()
    {
        using var client = await _factory.CreatePageClientAsync(AppModeWebFactory.BrowserUserAgent);
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", "appmode=1");

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();
        var doc = HtmlProbe.Parse(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("browser", HtmlProbe.AppMode(doc));
        Assert.Empty(HtmlProbe.WithScope(doc, "app"));
    }

    [Fact]
    public async Task App_user_agent_wins_over_a_stale_web_cookie()
    {
        using var client = await _factory.CreatePageClientAsync(AppModeWebFactory.AppUserAgent);
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", "appmode=0");

        var response = await client.SendAsync(request);
        var doc = HtmlProbe.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("inapp", HtmlProbe.AppMode(doc));
        Assert.Empty(HtmlProbe.WithScope(doc, "web"));
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task No_response_ever_sets_an_appmode_cookie(string name, string url)
    {
        foreach (var userAgent in new[] { AppModeWebFactory.AppUserAgent, AppModeWebFactory.BrowserUserAgent })
        {
            using var client = await _factory.CreatePageClientAsync(userAgent);
            var response = await client.GetAsync(url);

            var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.ToList()
                : new List<string>();

            foreach (var cookie in cookies.Where(c => c.Contains("appmode", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.True(cookie.Contains("appmode=;") || cookie.Contains("appmode=\""),
                    $"«{name}» ({url}): هیچ پاسخی نباید کوکی «حالت اپ» بنویسد، ولی نوشت → {cookie}");
            }
        }
    }

    [Fact]
    public async Task Mode_is_decided_per_request_not_per_session()
    {
        // همان نشست (بدون کوکی، چون ورود آزمونی است) اما دو User-Agent متفاوت:
        // هر پاسخ باید حالت خودش را داشته باشد و هیچ حالتی به درخواست بعدی سرایت نکند.
        var appHome = HtmlProbe.Parse((await GetAsync("/", AppModeWebFactory.AppUserAgent)).Html);
        var browserHome = HtmlProbe.Parse((await GetAsync("/", AppModeWebFactory.BrowserUserAgent)).Html);
        var appAgain = HtmlProbe.Parse((await GetAsync("/", AppModeWebFactory.AppUserAgent)).Html);

        Assert.Equal("inapp", HtmlProbe.AppMode(appHome));
        Assert.Equal("browser", HtmlProbe.AppMode(browserHome));
        Assert.Equal("inapp", HtmlProbe.AppMode(appAgain));
        Assert.Empty(HtmlProbe.WithScope(appAgain, "web"));
    }

    public static IEnumerable<object[]> Pages =>
        TestPageCatalog.Pages.Select(p => new object[] { p.Name, p.Url });

    private async Task<(HttpResponseMessage Response, string Html)> GetAsync(string url, string userAgent)
    {
        using var client = await _factory.CreatePageClientAsync(userAgent);
        var response = await client.GetAsync(url);
        return (response, await response.Content.ReadAsStringAsync());
    }
}
