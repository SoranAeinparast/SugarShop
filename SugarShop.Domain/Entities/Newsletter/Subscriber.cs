using System;

namespace SugarShop.Domain.Entities.Newsletter
{
    public class Subscriber
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? UnsubscribeToken { get; set; }
    }
}