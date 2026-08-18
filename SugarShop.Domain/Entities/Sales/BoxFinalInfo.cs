using System;

namespace SugarShop.Domain.Entities.Sales
{
    public class BoxFinalInfo
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string BoxTitle { get; set; } = "";
        public int FinalWeightGrams { get; set; }
        public decimal FinalPrice { get; set; }
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Order? Order { get; set; }
    }
}