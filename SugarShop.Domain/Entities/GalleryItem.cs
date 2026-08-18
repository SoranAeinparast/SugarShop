using System;

namespace SugarShop.Domain.Entities
{
    public class GalleryItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string MediaType { get; set; } = "Image";
        public string FilePath { get; set; } = "";
        public string? ThumbnailPath { get; set; }
        public int? VideoDuration { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? Category { get; set; }
    }
}