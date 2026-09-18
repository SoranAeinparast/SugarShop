using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SugarShop.Web.Services.Sms
{
    /// <summary>نتیجه استعلام وضعیت تحویل یک پیامک از sms.ir.</summary>
    public class SmsDeliveryInfo
    {
        public long MessageId { get; set; }
        public string? Mobile { get; set; }
        public byte? DeliveryState { get; set; }
        public long? DeliveryDateTime { get; set; }
        public decimal? Cost { get; set; }
    }

    /// <summary>
    /// کلاینت REST وب‌سرویس sms.ir
    /// مستندات: https://sms.ir/rest-api/ (هدر احراز هویت: x-api-key)
    /// نکته مهم واحد اعتبار: GET /v1/credit عددی مثل 733.02 برمی‌گرداند که «تعداد پیامک» است
    /// نه تومان/ریال (پنل هم همان را «733 پیامک» نشان می‌دهد). هزینه هر ارسال (cost) هم بر حسب
    /// همان واحد (تعداد پیامک) است.
    /// </summary>
    public class SmsIrClient
    {
        private const string BaseUrl = "https://api.sms.ir/v1/";
        private readonly HttpClient _http;
        private readonly ILogger<SmsIrClient> _logger;

        public SmsIrClient(HttpClient http, ILogger<SmsIrClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        private void Configure(string apiKey)
        {
            _http.DefaultRequestHeaders.Remove("x-api-key");
            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        private static string Normalize(string phone)
        {
            var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
            if (digits.StartsWith("0098")) digits = "0" + digits.Substring(4);
            if (digits.StartsWith("98") && digits.Length >= 12) digits = "0" + digits.Substring(2);
            if (digits.Length == 10 && digits.StartsWith("9")) digits = "0" + digits;
            return digits;
        }

        private async Task<(bool ok, JsonElement data, string? error)> SendAsync(HttpRequestMessage req)
        {
            try
            {
                // ثبت درخواست برای عیب‌یابی (بدون هدر کلید API)
                // ⚠️ ReadAsStringAsync مصرف‌کننده است؛ پس محتوا را بافر و دوباره ست می‌کنیم تا ارسال واقعی خراب نشود
                if (req.Content != null)
                {
                    var reqBody = await req.Content.ReadAsStringAsync();
                    req.Content = new StringContent(reqBody, Encoding.UTF8, "application/json");
                    _logger.LogInformation("sms.ir request {Method} {Url}: {Body}", req.Method, req.RequestUri, Truncate(reqBody, 500));
                }

                var res = await _http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                _logger.LogInformation("sms.ir response {Status}: {Body}", (int)res.StatusCode, Truncate(body, 800));

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                // پاسخ استاندارد sms.ir: { "status": 1, "message": "موفق", "data": {...} }
                int status = root.TryGetProperty("status", out var st) ? st.GetInt32() : (res.IsSuccessStatusCode ? 1 : 0);
                string? message = root.TryGetProperty("message", out var msg) ? msg.GetString() : null;
                var data = root.TryGetProperty("data", out var d) && d.ValueKind != JsonValueKind.Null ? d.Clone() : default;

                if (status == 1 && res.IsSuccessStatusCode) return (true, data, null);
                return (false, default, $"کد {(int)res.StatusCode}/{status}: {message ?? Truncate(body)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "sms.ir request failed");
                return (false, default, ex.Message);
            }
        }

        private static string Truncate(string? s, int max = 200)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

        /// <summary>ارسال تکی / انبوه با متن آزاد. cost پاسخ (تعداد پیامک مصرفی) و messageId هر گیرنده استخراج می‌شود.</summary>
        public async Task<SmsSendResult> SendBulkAsync(string apiKey, string senderLine, IReadOnlyList<string> phones, string message)
        {
            if (phones == null || phones.Count == 0) return SmsSendResult.Fail("فهرست شماره‌ها خالی است.");
            var normalized = phones.Select(Normalize).Where(p => p.Length >= 10).Distinct().ToList();
            if (normalized.Count == 0) return SmsSendResult.Fail("هیچ شماره معتبری یافت نشد.");

            Configure(apiKey);
            var payload = new { lineNumber = ToLine(senderLine), messageText = message, mobiles = normalized };
            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "send/bulk")
            {
                Content = JsonContent.Create(payload, options: JsonOpts)
            };
            var (ok, data, error) = await SendAsync(req);
            if (!ok) return SmsSendResult.Fail(error!);

            decimal cost = 0;
            long firstMessageId = 0;
            try
            {
                if (data.ValueKind == JsonValueKind.Object)
                {
                    if (data.TryGetProperty("cost", out var c) && c.ValueKind == JsonValueKind.Number)
                        cost = c.GetDecimal();
                    if (data.TryGetProperty("messageIds", out var ids) && ids.ValueKind == JsonValueKind.Array
                        && ids.GetArrayLength() > 0 && ids[0].ValueKind == JsonValueKind.Number)
                        firstMessageId = ids[0].GetInt64();
                    else if (data.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.Number)
                        firstMessageId = mid.GetInt64();
                }
            }
            catch { /* استخراج هزینه/شناسه اختیاری است — خود ارسال موفق بوده */ }

            return SmsSendResult.Ok(cost, firstMessageId > 0 ? firstMessageId.ToString() : null);
        }

        /// <summary>ارسال با الگوی تأییدشده (برای OTP). templateId الگوی ثبت‌شده در پنل sms.ir است.</summary>
        public async Task<SmsSendResult> SendVerifyAsync(string apiKey, string phone, int templateId, params (string name, string value)[] parameters)
        {
            var normalized = Normalize(phone);
            if (normalized.Length < 10) return SmsSendResult.Fail("شماره موبایل نامعتبر است.");

            Configure(apiKey);
            var payload = new
            {
                mobile = normalized,
                templateId,
                parameters = parameters.Select(p => new { name = p.name, value = p.value }).ToArray()
            };
            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "send/verify")
            {
                Content = JsonContent.Create(payload, options: JsonOpts)
            };
            var (ok, data, error) = await SendAsync(req);
            if (!ok) return SmsSendResult.Fail(error!);

            decimal cost = 0;
            try
            {
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("cost", out var c) && c.ValueKind == JsonValueKind.Number)
                    cost = c.GetDecimal();
            }
            catch { }
            return SmsSendResult.Ok(cost, data.ValueKind == JsonValueKind.Object && data.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.Number ? mid.GetInt64().ToString() : null);
        }

        /// <summary>
        /// اعتبار حساب — بر حسب «تعداد پیامک» (همان عددی که پنل sms.ir به‌عنوان «X پیامک» نشان می‌دهد).
        /// در خطا null برمی‌گردد.
        /// </summary>
        public async Task<decimal?> GetCreditAsync(string apiKey)
        {
            Configure(apiKey);
            var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "credit");
            var (ok, data, _) = await SendAsync(req);
            if (!ok) return null;
            try
            {
                if (data.ValueKind == JsonValueKind.Undefined || data.ValueKind == JsonValueKind.Null) return null;
                if (data.ValueKind == JsonValueKind.Number) return data.GetDecimal();
                foreach (var prop in data.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                        return prop.Value.GetDecimal();
            }
            catch { }
            return null;
        }

        /// <summary>دریافت شماره خطوط پنل.</summary>
        public async Task<List<string>?> GetLinesAsync(string apiKey)
        {
            Configure(apiKey);
            // نکته: مسیر درست طبق مستندات sms.ir فقط "line" است (نه "lines")
            var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "line");
            var (ok, data, _) = await SendAsync(req);
            if (!ok) return null;
            try
            {
                var lines = new List<string>();
                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                        lines.Add(item.ValueKind == JsonValueKind.Number ? item.GetInt64().ToString() : item.GetString() ?? "");
                }
                else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("data", out var inner) && inner.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in inner.EnumerateArray())
                        lines.Add(item.ValueKind == JsonValueKind.Number ? item.GetInt64().ToString() : item.GetString() ?? "");
                }
                return lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            }
            catch { return null; }
        }

        /// <summary>وضعیت تحویل یک پیامک بر اساس شناسه پیام (GET /send/{messageId}).</summary>
        public async Task<SmsDeliveryInfo?> GetDeliveryAsync(string apiKey, long messageId)
        {
            Configure(apiKey);
            var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + $"send/{messageId}");
            var (ok, data, _) = await SendAsync(req);
            if (!ok || data.ValueKind != JsonValueKind.Object) return null;
            try
            {
                var info = new SmsDeliveryInfo { MessageId = messageId };
                if (data.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.Number) info.MessageId = mid.GetInt64();
                if (data.TryGetProperty("mobile", out var mob)) info.Mobile = mob.ValueKind == JsonValueKind.Number ? mob.GetInt64().ToString() : mob.GetString();
                if (data.TryGetProperty("deliveryState", out var ds) && ds.ValueKind == JsonValueKind.Number) info.DeliveryState = ds.GetByte();
                if (data.TryGetProperty("deliveryDateTime", out var dd) && dd.ValueKind == JsonValueKind.Number) info.DeliveryDateTime = dd.GetInt64();
                if (data.TryGetProperty("cost", out var c) && c.ValueKind == JsonValueKind.Number) info.Cost = c.GetDecimal();
                return info;
            }
            catch { return null; }
        }

        /// <summary>گزارش ارسال‌های امروز (GET /send/live) — برای نمایش «گزارش زنده پنل» و کشف خطاهای مخابراتی.</summary>
        public async Task<List<SmsDeliveryInfo>?> GetLiveReportAsync(string apiKey, int pageSize = 50)
        {
            Configure(apiKey);
            var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + $"send/live?pageSize={Math.Clamp(pageSize, 1, 100)}&pageNumber=1");
            var (ok, data, _) = await SendAsync(req);
            if (!ok || data.ValueKind != JsonValueKind.Array) return null;
            try
            {
                var list = new List<SmsDeliveryInfo>();
                foreach (var item in data.EnumerateArray())
                {
                    var info = new SmsDeliveryInfo();
                    if (item.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.Number) info.MessageId = mid.GetInt64();
                    if (item.TryGetProperty("mobile", out var mob)) info.Mobile = mob.ValueKind == JsonValueKind.Number ? mob.GetInt64().ToString() : mob.GetString();
                    if (item.TryGetProperty("deliveryState", out var ds) && ds.ValueKind == JsonValueKind.Number) info.DeliveryState = ds.GetByte();
                    if (item.TryGetProperty("deliveryDateTime", out var dd) && dd.ValueKind == JsonValueKind.Number) info.DeliveryDateTime = dd.GetInt64();
                    if (item.TryGetProperty("cost", out var c) && c.ValueKind == JsonValueKind.Number) info.Cost = c.GetDecimal();
                    list.Add(info);
                }
                return list;
            }
            catch { return null; }
        }

        private static long ToLine(string senderLine)
            => long.TryParse(new string((senderLine ?? "").Where(char.IsDigit).ToArray()), out var l) ? l : 0L;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}
