using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SugarShop.Domain.Entities.Sales
{
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public int OrderId { get; set; }
        public Order? Order { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        [Precision(18, 2)]
        public decimal Subtotal { get; set; }

        [Precision(18, 2)]
        public decimal DiscountAmount { get; set; }

        [Precision(18, 2)]
        public decimal DeliveryFee { get; set; }

        [Precision(18, 2)]
        public decimal TaxAmount { get; set; }

        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }

        public string? Notes { get; set; }
        public bool IsPrinted { get; set; } = false;
        public DateTime? PrintedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<InvoiceItem> Items { get; set; } = new();
    }
}