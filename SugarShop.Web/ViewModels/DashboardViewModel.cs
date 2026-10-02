using SugarShop.Domain.Entities.Sales;

namespace SugarShop.Web.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalOrders { get; set; }
        public int PendingPaymentOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public decimal WalletBalance { get; set; }
        public int OpenTickets { get; set; }
        public Order? LastOrder { get; set; }
        public CustomCakeOrder? LastCakeOrder { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string? AvatarPath { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Gender { get; set; }
        public Address? DefaultAddress { get; set; }
    }
}