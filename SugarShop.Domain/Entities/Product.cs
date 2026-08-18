namespace SugarShop.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string TitleFa { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? WeightGrams { get; set; }
        public string? ImagePath { get; set; } 
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public int Inventory { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}