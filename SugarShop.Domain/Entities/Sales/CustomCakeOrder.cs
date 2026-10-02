using System;

namespace SugarShop.Domain.Entities.Sales
{
    public enum CustomCakeOrderStatus
    {
        Pending,
        Accepted,
        Rejected,
        InProduction,
        Ready,
        Completed
    }

    public class CustomCakeOrder
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public CustomCakeOrderStatus Status { get; set; } = CustomCakeOrderStatus.Pending;
        public string? AdminNotes { get; set; }
        public decimal? FinalPrice { get; set; }
        public bool IsPaid { get; set; } = false;
        public int? WeightGrams { get; set; } 
        public string? Flavor { get; set; }
        public string? Shape { get; set; }
        public string? Ingredients { get; set; }
        public string? Occasion { get; set; }
        public int? Servings { get; set; }
        public string? SpecialRequests { get; set; }
        public string? SampleImagePath { get; set; }
        public string? PrintImagePath { get; set; }
        public DateTime? DesiredDeliveryDateTime { get; set; }

        // ✅ تحویل: روش دریافت (در محل / پیک)، آدرس و هزینه پیک
        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Pickup;
        public int? AddressId { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? CustomerFullAddress { get; set; }
        public string? CustomerPostalCode { get; set; }
        public decimal? DeliveryFee { get; set; }
    }
}