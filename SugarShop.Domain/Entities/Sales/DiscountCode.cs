using System;

namespace SugarShop.Domain.Entities.Sales
{
    public enum DiscountType
    {
        Percentage = 1,
        FixedAmount = 2
    }

    public class DiscountCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = ""; 
        public string? Description { get; set; }

        public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
        public decimal DiscountValue { get; set; }
        public decimal? MinimumOrderAmount { get; set; } 

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; } 
        public int? UsageLimit { get; set; } 
        public int UsedCount { get; set; } = 0; 

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}