using System;
using System.Collections.Generic;

namespace SugarShop.Domain.Entities.Sales
{
    public enum OrderStatus
    {
        AwaitingReview = 1,
        PendingPayment = 2,
        Paid = 3,
        Preparing = 4,
        Shipped = 5,
        Delivered = 6,
        Cancelled = 7
    }

    public enum PaymentStatus
    {
        Unpaid = 1,
        Initiated = 2,
        Succeeded = 3,
        Failed = 4
    }

    // ✅ enum جدید برای روش تحویل
    public enum DeliveryMethod
    {
        Pickup = 1,      // دریافت در محل
        Delivery = 2     // ارسال با پیک
    }

    public class Order
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = "";
        public string? UserId { get; set; }
        public OrderStatus OrderStatus { get; set; } = OrderStatus.PendingPayment;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
        public decimal TotalAmountSnapshot { get; set; }
        public decimal? DiscountAmountSnapshot { get; set; }
        public decimal DeliveryFeeSnapshot { get; set; }
        public decimal? TaxAmountSnapshot { get; set; }
        public decimal? FinalTotalAmount { get; set; }
        public int? FinalTotalWeightGrams { get; set; }
        public bool IsPaymentEnabled { get; set; } = false;

        /// <summary>
        /// زمان ارسال یادآوری پیامکیِ پرداخت مرحله دوم (UTC). مقدار غیر null یعنی برای این
        /// سفارش یک‌بار یادآوری رفته است. این نشانه روی خود سفارش نگه داشته می‌شود تا حتی اگر
        /// لاگ یادآوری‌ها (که برای پاکسازی دوره‌ای ساخته شده) پاک شود، یادآوری دوباره فرستاده نشود.
        /// </summary>
        public DateTime? PaymentReminderSentAt { get; set; }
        public string? AdminNotes { get; set; }

        /// <summary>
        /// زمان کسر موجودی انبار برای این سفارش. مقدار غیر null یعنی موجودی همین سفارش
        /// یک‌بار کم شده است؛ در پرداخت مرحله‌ای (پیش‌پرداخت + تسویه پس از وزن‌کشی)
        /// جلوی کسر دوباره را می‌گیرد.
        /// </summary>
        public DateTime? InventoryDeductedAt { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public int? DiscountCodeId { get; set; }
        public DiscountCode? DiscountCode { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string CustomerFullAddress { get; set; } = "";
        public string? CustomerPostalCode { get; set; }
        public int? CustomerAddressId { get; set; }
        public Address? CustomerAddress { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public TimeSpan? DeliveryTime { get; set; }
        public string? Notes { get; set; }

        // ✅ فیلد جدید: روش تحویل
        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Pickup;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<OrderItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
    }
}