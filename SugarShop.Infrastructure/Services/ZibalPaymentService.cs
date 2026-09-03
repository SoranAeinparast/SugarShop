using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace SugarShop.Infrastructure.Services
{
    public class ZibalPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _merchant;
        private readonly string _requestUrl;
        private readonly string _startUrl;
        private readonly string _verifyUrl;
        private readonly ILogger<ZibalPaymentService> _logger;

        public ZibalPaymentService(HttpClient httpClient, IConfiguration configuration, ILogger<ZibalPaymentService> logger)
        {
            _httpClient = httpClient;
            _merchant = configuration["Zibal:Merchant"] ?? "zibal";
            _requestUrl = "https://gateway.zibal.ir/v1/request";
            _startUrl = "https://gateway.zibal.ir/start/";
            _verifyUrl = "https://gateway.zibal.ir/v1/verify";
            _logger = logger;
        }

        public async Task<(bool Success, string TrackId, string PaymentUrl, string ErrorMessage)> RequestPayment(long amountInRials, string description, string callbackUrl, string mobile = null, string merchant = null)
        {
            var requestData = new
            {
                merchant = merchant ?? _merchant,
                amount = amountInRials,
                callbackUrl = callbackUrl,
                description = description,
                mobile = mobile ?? ""
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_requestUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Zibal RequestPayment response: {Response}", responseString);

            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("result", out var resultElement) && resultElement.GetInt32() == 100)
                {
                    if (root.TryGetProperty("trackId", out var trackIdElement))
                    {
                        string trackId = trackIdElement.ValueKind == JsonValueKind.String
                            ? trackIdElement.GetString() ?? ""
                            : trackIdElement.GetInt64().ToString();

                        return (true, trackId, _startUrl + trackId, null);
                    }
                    return (false, "", "", "trackId دریافت نشد");
                }
                else
                {
                    string errorMessage = root.TryGetProperty("message", out var msgElem) ? msgElem.GetString() ?? "خطا در اتصال به درگاه زیبال" : "خطا در اتصال به درگاه زیبال";
                    return (false, "", "", errorMessage);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Zibal request response");
                return (false, "", "", $"خطا در تجزیه پاسخ: {ex.Message}");
            }
        }

        public async Task<(bool Success, long RefNumber, string ErrorMessage)> VerifyPayment(string trackId, string merchant = null)
        {
            var verifyData = new
            {
                merchant = merchant ?? _merchant,
                trackId = trackId
            };

            var json = JsonSerializer.Serialize(verifyData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_verifyUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Zibal Verify response: {Response}", responseString);

            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("result", out var resultElement) && resultElement.GetInt32() == 100)
                {
                    if (root.TryGetProperty("refNumber", out var refElement) && refElement.ValueKind != JsonValueKind.Null)
                    {
                        long refNumber = refElement.GetInt64();
                        return (true, refNumber, null);
                    }
                    return (true, 0, null);
                }
                else
                {
                    string errorMessage = root.TryGetProperty("message", out var msgElem) ? msgElem.GetString() ?? "تراکنش ناموفق" : "تراکنش ناموفق";
                    if (root.TryGetProperty("result", out var errCodeElem))
                        errorMessage = $"کد خطا: {errCodeElem.GetInt32()} - {errorMessage}";
                    return (false, 0, errorMessage);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Zibal verify response");
                return (false, 0, $"خطا در تجزیه پاسخ تأیید: {ex.Message}");
            }
        }
    }
}
