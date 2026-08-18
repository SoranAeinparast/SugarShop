using System;

namespace SugarShop.Domain.Entities
{
    public class ContentSource
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; } = true;
        public int Priority { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}