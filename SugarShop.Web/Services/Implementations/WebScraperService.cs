using Microsoft.Extensions.Logging;
using SugarShop.Web.Services.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SugarShop.Web.Services.Implementations
{
    public class WebScraperService : IWebScraperService
    {
        private const int MaxResponseBytes = 5 * 1024 * 1024;
        private const int MaxRedirects = 4;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebScraperService> _logger;

        public WebScraperService(IHttpClientFactory httpClientFactory, ILogger<WebScraperService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public static SocketsHttpHandler CreateSafeHandler()
        {
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                UseProxy = false,
                ConnectCallback = ConnectToPublicAddressAsync
            };
        }

        public async Task<string> FetchHtmlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be empty", nameof(url));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException("A valid absolute URL is required.", nameof(url));
            ValidateUri(uri);

            var client = _httpClientFactory.CreateClient("SafeScraper");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SugarShop/1.0 ContentFetcher");
            client.Timeout = TimeSpan.FromSeconds(15);

            for (var redirects = 0; ; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (IsRedirect(response.StatusCode))
                {
                    if (redirects >= MaxRedirects || response.Headers.Location == null)
                        throw new InvalidOperationException("The source redirected too many times or returned an invalid redirect.");

                    uri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(uri, response.Headers.Location);
                    ValidateUri(uri);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"The source returned HTTP {(int)response.StatusCode}.");
                if (response.Content.Headers.ContentLength > MaxResponseBytes)
                    throw new InvalidOperationException("The source response exceeds the 5 MiB limit.");

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var content = new MemoryStream();
                var buffer = new byte[81920];
                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    if (read == 0) break;
                    if (content.Length + read > MaxResponseBytes)
                        throw new InvalidOperationException("The source response exceeds the 5 MiB limit.");
                    await content.WriteAsync(buffer.AsMemory(0, read));
                }

                _logger.LogDebug("Fetched content from {Host} ({Bytes} bytes).", uri.IdnHost, content.Length);
                return System.Text.Encoding.UTF8.GetString(content.ToArray());
            }
        }

        private static bool IsRedirect(HttpStatusCode status)
            => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect
                or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect;

        private static void ValidateUri(Uri uri)
        {
            if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException("Only credential-free HTTP/HTTPS URLs on their default ports are allowed.");

            if (IPAddress.TryParse(uri.Host, out var address) && !IsPublicInternetAddress(address))
                throw new InvalidOperationException("Access to non-public IP addresses is blocked.");
        }

        private static async ValueTask<Stream> ConnectToPublicAddressAsync(
            SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            if (addresses.Length == 0 || addresses.Any(address => !IsPublicInternetAddress(address)))
                throw new HttpRequestException("The source host does not resolve exclusively to public IP addresses.");

            Exception? lastError = null;
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    socket.Dispose();
                    lastError = ex;
                }
            }

            throw new HttpRequestException("Could not connect to the public source host.", lastError);
        }

        private static bool IsPublicInternetAddress(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)
                || ip.Equals(IPAddress.IPv6None) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal
                || ip.IsIPv6Multicast || ip.IsIPv6Teredo)
                return false;

            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var a = bytes[0];
                var b = bytes[1];
                if (a == 0 || a == 10 || a == 127 || a >= 224) return false;
                if (a == 100 && b >= 64 && b <= 127) return false;
                if (a == 169 && b == 254) return false;
                if (a == 172 && b >= 16 && b <= 31) return false;
                if (a == 192 && (b == 0 || b == 168)) return false;
                if (a == 192 && b == 88 && bytes[2] == 99) return false;
                if (a == 198 && (b == 18 || b == 19 || (b == 51 && bytes[2] == 100))) return false;
                if (a == 203 && b == 0 && bytes[2] == 113) return false;
                return true;
            }

            // Only global-unicast IPv6 is allowed; exclude documentation, 6to4 and Teredo ranges.
            return ip.AddressFamily == AddressFamily.InterNetworkV6
                && (bytes[0] & 0xE0) == 0x20
                && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
                && !(bytes[0] == 0x20 && bytes[1] == 0x02);
        }
    }
}
