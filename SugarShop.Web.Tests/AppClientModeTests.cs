using Microsoft.AspNetCore.Http;
using SugarShop.Web.Helpers;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// تنها منبع حقیقتِ «این درخواست از اپ آمده یا از مرورگر؟» — <see cref="AppClient.IsAppRequest"/>.
/// هر تغییری در این تصمیم باید از این آزمون‌ها بگذرد: نه کوکی، نه هدر دیگر و نه حافظه‌ی سمت کلاینت.
/// </summary>
public class AppClientModeTests
{
    [Fact]
    public void Empty_or_missing_user_agent_is_browser()
    {
        Assert.False(AppClient.IsAppRequest(null));
        Assert.False(AppClient.IsAppRequest(ContextWithUserAgent(string.Empty)));
        Assert.False(AppClient.IsAppRequest(ContextWithUserAgent("   ")));
    }

    [Fact]
    public void Android_shell_user_agent_is_app()
    {
        Assert.True(AppClient.IsAppRequest(ContextWithUserAgent(AppModeWebFactory.AppUserAgent)));
        // نشانه، مستقل از بزرگی/کوچکی حروف است
        Assert.True(AppClient.IsAppRequest(ContextWithUserAgent("Mozilla/5.0 sugarshopapp/1.4")));
    }

    [Theory]
    [InlineData(AppModeWebFactory.BrowserUserAgent)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Safari/604.1")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/121.0 Mobile Safari/537.36")]
    [InlineData("Mozilla/5.0 (Linux; Android 13; wv) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36")]
    public void Browsers_and_pwa_are_not_app(string userAgent)
        => Assert.False(AppClient.IsAppRequest(ContextWithUserAgent(userAgent)));

    [Fact]
    public void Cookie_alone_never_makes_a_request_app()
    {
        // کوکی appmode نسخه‌های قبلی باید کاملاً بی‌اثر باشد
        var context = ContextWithUserAgent(AppModeWebFactory.BrowserUserAgent);
        context.Request.Headers.Cookie = "appmode=1";
        Assert.False(AppClient.IsAppRequest(context));
    }

    [Fact]
    public void App_user_agent_works_even_with_a_stale_web_cookie()
    {
        var context = ContextWithUserAgent(AppModeWebFactory.AppUserAgent);
        context.Request.Headers.Cookie = "appmode=0";
        Assert.True(AppClient.IsAppRequest(context));
    }

    private static HttpContext ContextWithUserAgent(string userAgent)
    {
        var context = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(userAgent))
            context.Request.Headers.UserAgent = userAgent;
        return context;
    }
}
