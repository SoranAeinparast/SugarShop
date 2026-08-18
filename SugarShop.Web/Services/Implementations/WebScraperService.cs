using Microsoft.Extensions.Logging;
using SugarShop.Web.Services.Interfaces;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SugarShop.Web.Services.Implementations
{
    public class WebScraperService : IWebScraperService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebScraperService> _logger;

        private static readonly string[] BlockedHosts = { "localhost", "127.0.0.1", "0.0.0.0", "169.254.169.254", "::1", "metadata.google.internal" };

        public WebScraperService(IHttpClientFactory httpClientFactory, ILogger<WebScraperService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> FetchHtmlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be empty", nameof(url));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "http" && uri.Scheme != "https")
                throw new ArgumentException("Only http/https URLs are allowed", nameof(url));

            var host = uri.Host.ToLower();
            foreach (var blocked in BlockedHosts)
            {
                if (host.Contains(blocked))
                    throw new InvalidOperationException("Access to this host is blocked for security reasons.");
            }

            // Block private IP ranges
            if (IPAddress.TryParse(host, out var ip))
            {
                if (IsPrivateOrLoopback(ip))
                    throw new InvalidOperationException("Access to private IP addresses is blocked.");
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SugarShop/1.0 ContentFetcher");
            client.Timeout = TimeSpan.FromSeconds(15);

            var response = await client.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            // Limit response size to 5MB
            var content = await response.Content.ReadAsStringAsync();
            if (content.Length > 5 * 1024 * 1024)
            {
                content = content.Substring(0, 5 * 1024 * 1024);
                _logger.LogWarning("Truncated large response from {Url}", url);
            }

            return content;
        }

        private static bool IsPrivateOrLoopback(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;   // ← اصلاح شد

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = ip.GetAddressBytes();
                if (bytes[0] == 10) return true;                                // 10.0.0.0/8
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true; // 172.16.0.0/12
                if (bytes[0] == 192 && bytes[1] == 168) return true;            // 192.168.0.0/16
                if (bytes[0] == 169 && bytes[1] == 254) return true;            // 169.254.0.0/16
            }
            return false;
        }

    }
}
