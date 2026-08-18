namespace SugarShop.Domain.Entities
{
    public class Slider
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }

        public string? ImagePath { get; set; }
        public bool IsVideo { get; set; } = false;
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}