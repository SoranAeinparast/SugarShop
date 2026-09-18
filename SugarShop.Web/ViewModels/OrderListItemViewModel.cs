using SugarShop.Domain.Entities.Sales;

namespace SugarShop.Web.ViewModels
{
    public class OrderListItemViewModel
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = "";
        public OrderStatus OrderStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalFinalPrice { get; set; }
        public bool IsPaymentEnabled { get; set; }
        public bool IsDeletable { get; set; }

        // ✅ نمایش سفارش‌های کیک سفارشی در همان لیست «سفارشات من»
        public bool IsCustomCake { get; set; }
        public int? CustomCakeOrderId { get; set; }
        public string? CakeFlavor { get; set; }
        public CustomCakeOrderStatus? CakeStatus { get; set; }
        public bool? CakeIsPaid { get; set; }
        public decimal? CakeDeliveryFee { get; set; }
        public DeliveryMethod? CakeDeliveryMethod { get; set; }
    }
}