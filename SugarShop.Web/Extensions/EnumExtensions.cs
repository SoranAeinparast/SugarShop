using SugarShop.Domain.Entities.Sales;

namespace SugarShop.Web.Extensions
{
    public static class EnumExtensions
    {
        public static string ToFarsi(this OrderStatus status)
        {
            return status switch
            {
                OrderStatus.AwaitingReview => "در انتظار بررسی",
                OrderStatus.PendingPayment => "در انتظار پرداخت",
                OrderStatus.Paid => "پرداخت شده",
                OrderStatus.Preparing => "در حال آماده‌سازی",
                OrderStatus.Shipped => "ارسال شده",
                OrderStatus.Delivered => "تحویل شده",
                OrderStatus.Cancelled => "لغو شده",
                _ => "نامشخص"
            };
        }
        public static string ToFarsi(this PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Unpaid => "پرداخت نشده",
                PaymentStatus.Initiated => "در حال پرداخت",
                PaymentStatus.Succeeded => "پرداخت موفق",
                PaymentStatus.Failed => "پرداخت ناموفق",
                _ => "نامشخص"
            };
        }
    }
}