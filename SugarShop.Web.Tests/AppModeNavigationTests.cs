using System.Net;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// Keeps same-origin navigation in the presentation selected for that request, including
/// redirects and standalone application routes. This deliberately checks both client types.
/// </summary>
public sealed class AppModeNavigationTests : IClassFixture<AppModeWebFactory>
{
    private readonly AppModeWebFactory _factory;

    public AppModeNavigationTests(AppModeWebFactory factory) => _factory = factory;

    public static IEnumerable<object[]> PageModes => TestPageCatalog.Pages.SelectMany(page => new[]
    {
        new object[] { page.Name, page.Url, AppModeWebFactory.AppUserAgent, "inapp" },
        new object[] { page.Name, page.Url, AppModeWebFactory.BrowserUserAgent, "browser" }
    });

    [Theory]
    [MemberData(nameof(PageModes))]
    public async Task Same_origin_links_keep_the_starting_presentation(
        string pageName, string pageUrl, string userAgent, string expectedMode)
    {
        using var client = await _factory.CreatePageClientAsync(userAgent);
        await AssertPageLinksKeepPresentationAsync(client, pageName, pageUrl, expectedMode);
    }

    [Fact]
    public async Task Public_statement_back_links_keep_the_starting_presentation()
    {
        await _factory.EnsureSeededAsync();
        Assert.False(string.IsNullOrWhiteSpace(_factory.StatementToken));
        var pageUrl = "/s/" + _factory.StatementToken;

        foreach (var (userAgent, expectedMode) in new[]
                 {
                     (AppModeWebFactory.AppUserAgent, "inapp"),
                     (AppModeWebFactory.BrowserUserAgent, "browser")
                 })
        {
            using var client = await _factory.CreatePageClientAsync(userAgent);
            await AssertPageLinksKeepPresentationAsync(client, "Public statement", pageUrl, expectedMode);
        }
    }

    private static async Task AssertPageLinksKeepPresentationAsync(
        HttpClient client, string pageName, string pageUrl, string expectedMode)
    {
        using var sourceResponse = await client.GetAsync(pageUrl);
        Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);

        var source = HtmlProbe.Parse(await sourceResponse.Content.ReadAsStringAsync());
        var baseUri = client.BaseAddress!;
        var links = source.DocumentNode.SelectNodes("//a[@href]")?
            .Select(node => node.GetAttributeValue("href", string.Empty).Trim())
            .Where(href => href.Length > 0 && !href.StartsWith('#'))
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();

        foreach (var href in links)
        {
            if (!Uri.TryCreate(baseUri, href, out var target) || !IsSameOrigin(baseUri, target))
                continue;

            using var response = await GetFollowingSameOriginRedirectsAsync(client, target, baseUri);
            if (!response.IsSuccessStatusCode ||
                !string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase))
                continue;

            var destination = HtmlProbe.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expectedMode, HtmlProbe.AppMode(destination),
                $"{pageName} ({pageUrl}) link '{href}' reached {response.RequestMessage?.RequestUri} " +
                $"but rendered mode '{HtmlProbe.AppMode(destination)}' instead of '{expectedMode}'.");
        }
    }

    [Fact]
    public async Task App_short_route_redirects_keep_app_and_browser_presentation()
    {
        await _factory.EnsureSeededAsync();

        foreach (var (userAgent, expectedMode) in new[]
                 {
                     (AppModeWebFactory.AppUserAgent, "inapp"),
                     (AppModeWebFactory.BrowserUserAgent, "browser")
                 })
        {
            using var client = await _factory.CreatePageClientAsync(userAgent);
            foreach (var path in new[] { $"/o/{_factory.SampleOrderId}", $"/p/{_factory.SampleOrderId}" })
            {
                var baseUri = client.BaseAddress!;
                Uri redirectTarget;
                using (var initial = await client.GetAsync(path))
                {
                    Assert.InRange((int)initial.StatusCode, 300, 399);
                    var location = initial.Headers.Location;
                    Assert.NotNull(location);
                    var resolvedLocation = location!;
                    redirectTarget = resolvedLocation.IsAbsoluteUri
                        ? resolvedLocation
                        : new Uri(new Uri(baseUri, path), resolvedLocation);
                    Assert.True(IsSameOrigin(baseUri, redirectTarget),
                        $"The redirect from {path} left the site's origin.");
                }

                using var response = await GetFollowingSameOriginRedirectsAsync(client, redirectTarget, baseUri);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
                var doc = HtmlProbe.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(expectedMode, HtmlProbe.AppMode(doc),
                    $"The redirect chain from {path} lost the {expectedMode} presentation.");
            }
        }
    }

    private static async Task<HttpResponseMessage> GetFollowingSameOriginRedirectsAsync(
        HttpClient client, Uri start, Uri baseUri)
    {
        var current = start;
        for (var redirectCount = 0; redirectCount <= 5; redirectCount++)
        {
            var response = await client.GetAsync(current.PathAndQuery);
            var location = response.Headers.Location;
            if (location == null || (int)response.StatusCode is < 300 or >= 400)
                return response;

            var next = location.IsAbsoluteUri ? location : new Uri(current, location);
            if (!IsSameOrigin(baseUri, next))
                return response;

            response.Dispose();
            current = next;
        }

        throw new InvalidOperationException($"Too many redirects while navigating to {start}.");
    }

    private static bool IsSameOrigin(Uri expected, Uri actual)
        => (actual.Scheme == Uri.UriSchemeHttp || actual.Scheme == Uri.UriSchemeHttps)
           && string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase)
           && string.Equals(expected.Authority, actual.Authority, StringComparison.OrdinalIgnoreCase);
}
