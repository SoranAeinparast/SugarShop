using System;

namespace SugarShop.Domain.Entities
{
    public class MenuItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public int? ParentId { get; set; }
        public int Order { get; set; } = 0;
        public string Location { get; set; } = "header";
        public string? Icon { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}