using SugarShop.Domain.Entities.Sales;

namespace SugarShop.Web.ViewModels
{
    /// <summary>
    /// یک ردیف از جدول «مدیریت سفارشات» در پنل مدیریت.
    ///
    /// این مدل عمداً فقط ستون‌هایی را دارد که جدول نمایش می‌دهد تا صفحه‌بندی سمت سرور
    /// سبک بماند: به‌جای مادیت‌کردن کل موجودیت <c>Order</c> (با آدرس، یادداشت‌ها و فیلدهای
    /// سنگین دیگر)، فقط همین چند ستون از دیتابیس خوانده می‌شود.
    /// </summary>
    public class AdminOrderListItemViewModel
    {
        public int Id { get; set; }

        public string OrderCode { get; set; } = "";

        /// <summary>برای واکشی نام کاربر از Identity (فقط برای ردیف‌های همین صفحه).</summary>
        public string? UserId { get; set; }

        public string CustomerName { get; set; } = "";

        public OrderStatus OrderStatus { get; set; }

        public decimal? FinalTotalAmount { get; set; }

        public int? FinalTotalWeightGrams { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
