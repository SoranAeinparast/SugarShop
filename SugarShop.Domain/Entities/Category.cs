namespace SugarShop.Domain.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string TitleFa { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? ImagePath { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public bool RequiresBoxSelection { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}