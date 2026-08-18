using System;

namespace SugarShop.Web.SessionModels
{
    public class CheckoutSessionState
    {
        public const string SessionKey = "CHECKOUT_SESSION_STATE";
        public string? Title { get; set; }
        public string? FullAddress { get; set; }
        public string? PostalCode { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public TimeSpan? DeliveryTime { get; set; }
        public string? Notes { get; set; }
    }
}