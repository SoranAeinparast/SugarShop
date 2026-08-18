using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SugarShop.Domain.Entities.Sales
{
    public class Payment
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string Provider { get; set; } = "Zarinpal";
        public string MerchantRefId { get; set; } = "";
        public string Authority { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "IRR";
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
        public string? RawRequest { get; set; }
        public string? RawResponse { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Order? Order { get; set; }
        public string TransactionCode { get; set; }
    }
}