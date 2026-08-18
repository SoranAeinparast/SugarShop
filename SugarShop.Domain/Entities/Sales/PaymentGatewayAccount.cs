using System;
using System.Text.Json;

namespace SugarShop.Domain.Entities.Sales
{
    public class PaymentGatewayAccount
    {
        public int Id { get; set; }
        public int GatewayId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string ConfigData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public virtual PaymentGateway? Gateway { get; set; }
    }
}