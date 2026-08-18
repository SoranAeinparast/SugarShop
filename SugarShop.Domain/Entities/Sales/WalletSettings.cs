using System;

namespace SugarShop.Domain.Entities.Sales
{
    public class WalletSettings
    {
        public int Id { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string ReturnType { get; set; } = "Percentage";
        public decimal ReturnValue { get; set; } = 0;
        public decimal MinimumOrderAmount { get; set; } = 0;
        public bool AllowDirectRecharge { get; set; } = true;
        public decimal DirectRechargeMinAmount { get; set; } = 10000;
        public decimal DirectRechargeMaxAmount { get; set; } = 0;
        public decimal MaxWalletBalance { get; set; } = 0;
        public int ExpiryDays { get; set; } = 0;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}