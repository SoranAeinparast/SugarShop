namespace SugarShop.Web.ViewModels
{
    public class CategoryProductsViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryTitle { get; set; } = "";
        public string CategorySlug { get; set; } = "";
        public bool RequiresBoxSelection { get; set; }
        public List<ProductCardViewModel> Products { get; set; } = new();
    }

    public class ProductCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? ImagePath { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int Inventory { get; set; }
        public bool IsSweet { get; set; }
        public int ApproxWeightGrams { get; set; }
        public decimal PricePerKg { get; set; }
        public decimal UnitPrice { get; set; }
        public int? WeightGrams { get; set; }
    }
}