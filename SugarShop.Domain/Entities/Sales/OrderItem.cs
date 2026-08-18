using System;

namespace SugarShop.Domain.Entities.Sales
{
    public enum OrderItemType
    {
        Product = 1,
        SweetItem = 2
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public OrderItemType ItemType { get; set; }
        public int? ProductId { get; set; }
        public int? SweetItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPriceSnapshot { get; set; }
        public int? WeightSnapshotGrams { get; set; }
        public decimal TotalPriceSnapshot { get; set; }
        public int? BoxTypeId { get; set; }
        public string? BoxTitle { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Order? Order { get; set; }
    }
}