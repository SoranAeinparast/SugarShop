using System;

namespace SugarShop.Domain.Entities
{
    public class SplashSetting
    {
        public int Id { get; set; }
        public string VideoPath { get; set; } = "";
        public string ButtonText { get; set; } = "ورود";
        public bool IsEnabled { get; set; } = true;
        public bool IsMuted { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}