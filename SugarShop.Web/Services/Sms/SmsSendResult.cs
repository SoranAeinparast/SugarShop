using System;

namespace SugarShop.Web.Services.Sms
{
    /// <summary>نتیجه یک ارسال پیامک.</summary>
    public class SmsSendResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal Cost { get; set; }
        public string? ProviderMessageId { get; set; }

        public static SmsSendResult Ok(decimal cost = 0, string? messageId = null)
            => new() { Success = true, Cost = cost, ProviderMessageId = messageId };

        public static SmsSendResult Fail(string error)
            => new() { Success = false, ErrorMessage = error };
    }
}
