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
    }
}