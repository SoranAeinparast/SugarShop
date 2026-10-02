using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SugarShop.Infrastructure.Services
{
    public class ZarinPalPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _merchantId;
        private readonly bool _isSandbox;

        public ZarinPalPaymentService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _merchantId = configuration["Zarinpal:MerchantId"];
            _isSandbox = configuration["Zarinpal:ZarinpalMode"] == "Sandbox";
        }

        public async Task<(bool Success, string Authority, string PaymentUrl, string ErrorMessage)> RequestPayment(long amount, string description, string callbackUrl)
        {
            var requestUrl = _isSandbox
                ? "https://sandbox.zarinpal.com/pg/v4/payment/request.json"
                : "https://api.zarinpal.com/pg/v4/payment/request.json";

            var requestData = new
            {
                merchant_id = _merchantId,
                amount = amount,
                callback_url = callbackUrl,
                description = description
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(requestUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            try
            {
                var result = JsonSerializer.Deserialize<ZarinpalRequestResponse>(responseString);
                if (result?.Data?.Code == 100 && !string.IsNullOrEmpty(result.Data.Authority))
                {
                    var paymentUrl = _isSandbox
                        ? $"https://sandbox.zarinpal.com/pg/StartPay/{result.Data.Authority}"
                        : $"https://www.zarinpal.com/pg/StartPay/{result.Data.Authority}";
                    return (true, result.Data.Authority, paymentUrl, null!);
                }
                return (false, null!, null!, result?.Errors?.Message ?? "خطا در اتصال به درگاه پرداخت");
            }
            catch
            {
                return (false, null!, null!, "پاسخ نامعتبر از درگاه پرداخت");
            }
        }

        public async Task<(bool Success, long RefId, string ErrorMessage)> VerifyPayment(string authority, long amount)
        {
            var verifyUrl = _isSandbox
                ? "https://sandbox.zarinpal.com/pg/v4/payment/verify.json"
                : "https://api.zarinpal.com/pg/v4/payment/verify.json";

            var verifyData = new
            {
                merchant_id = _merchantId,
                amount = amount,
                authority = authority
            };

            var content = new StringContent(JsonSerializer.Serialize(verifyData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(verifyUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            try
            {
                var result = JsonSerializer.Deserialize<ZarinpalVerifyResponse>(responseString);
                if (result?.Data?.Code == 100)
                {
                    return (true, result.Data.RefId, null!);
                }
                return (false, 0, result?.Errors?.Message ?? "تراکنش ناموفق بود");
            }
            catch
            {
                return (false, 0, "خطا در بررسی وضعیت تراکنش");
            }
        }
    }

    public class ZarinpalRequestResponse { public ZarinpalRequestData Data { get; set; } = null!; public ZarinpalError Errors { get; set; } = null!; }
    public class ZarinpalRequestData { public int Code { get; set; } public string Authority { get; set; } = null!; }
    public class ZarinpalVerifyResponse { public ZarinpalVerifyData Data { get; set; } = null!; public ZarinpalError Errors { get; set; } = null!; }
    public class ZarinpalVerifyData { public int Code { get; set; } public long RefId { get; set; } }
    public class ZarinpalError { public string Message { get; set; } = null!; }
}