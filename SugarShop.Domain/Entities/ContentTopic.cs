using System;

namespace SugarShop.Domain.Entities
{
    public class ContentTopic
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Keywords { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}