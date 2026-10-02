using Microsoft.Extensions.Logging.Abstractions;
using SugarShop.Web.Services.Implementations;
using Xunit;

namespace SugarShop.Web.Tests;

public sealed class WebScraperServiceSafetyTests
{
    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://192.168.1.10")]
    [InlineData("http://169.254.169.254")]
    [InlineData("http://user:password@example.com")]
    [InlineData("http://example.com:8080")]
    [InlineData("file:///etc/passwd")]
    public async Task Unsafe_or_non_http_urls_are_rejected_before_connecting(string url)
    {
        var clientFactory = new NoNetworkHttpClientFactory();
        var service = new WebScraperService(clientFactory, NullLogger<WebScraperService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchHtmlAsync(url));
        Assert.Equal(0, clientFactory.CreateCount);
    }

    private sealed class NoNetworkHttpClientFactory : IHttpClientFactory
    {
        public int CreateCount { get; private set; }

        public HttpClient CreateClient(string name)
        {
            CreateCount++;
            throw new InvalidOperationException("The safety test must not make network requests.");
        }
    }
}
