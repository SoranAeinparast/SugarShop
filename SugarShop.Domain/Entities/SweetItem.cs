using System;

namespace SugarShop.Domain.Entities
{
    public class SweetItem
    {
        public int Id { get; set; }
        public string TitleFa { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? ImagePath { get; set; }
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public int ApproxWeightGrams { get; set; }
        public decimal PricePerKg { get; set; }
        public int InventoryCount { get; set; }
        public bool IsInStock => InventoryCount > 0;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public decimal ApproxPricePerPiece => (PricePerKg * ApproxWeightGrams) / 1000m;
    }
}