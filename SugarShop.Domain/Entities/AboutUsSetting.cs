using System;

namespace SugarShop.Domain.Entities
{
    public class AboutUsSetting
    {
        public int Id { get; set; }
        public string? HeroTitle { get; set; }
        public string? HeroDescription { get; set; }
        public string? HeroImagePath { get; set; }
        public string? StoryTitle { get; set; }
        public string? StoryContent { get; set; }
        public string? StoryImagePath { get; set; }
        public string? VideoTitle { get; set; }
        public string? VideoPath { get; set; }
        public string? ValuesJson { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}