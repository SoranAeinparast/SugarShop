using System;
using System.Collections.Generic;

namespace SugarShop.Domain.Entities.Sales
{
    public class PaymentGateway
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string GatewayType { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public virtual ICollection<PaymentGatewayAccount> Accounts { get; set; } = new List<PaymentGatewayAccount>();
    }
}