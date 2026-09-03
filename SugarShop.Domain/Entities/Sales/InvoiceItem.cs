using Microsoft.EntityFrameworkCore;

namespace SugarShop.Domain.Entities.Sales
{
    public class InvoiceItem
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }
        public string ItemName { get; set; } = "";
        public int Quantity { get; set; }

        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        [Precision(18, 2)]
        public decimal TotalPrice { get; set; }

        public int? WeightGrams { get; set; }
        public string? BoxTitle { get; set; }
    }
}