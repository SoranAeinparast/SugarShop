using System;

namespace SugarShop.Domain.Entities
{
    public class Wallet
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public decimal Balance { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WalletTransaction
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public decimal Amount { get; set; }
        public string Type { get; set; } = "";
        public string? Description { get; set; }
        public int? OrderId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}