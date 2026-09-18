using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// نتیجه محاسبه مالی یک سفارش. همه اعداد به «تومان» و بر مبنای وضعیت فعلی سفارش هستند
    /// (در پرداخت مرحله‌ای، جعبه‌های وزن‌کشی‌نشده در مبلغ لحاظ نمی‌شوند).
    /// </summary>
    public sealed class OrderPricing
    {
        /// <summary>جمع محصولات قیمت‌ثابت (برای سفارش‌های موقت مثل کیک: مبلغ نهایی اعلام‌شده).</summary>
        public decimal ProductTotal { get; init; }

        /// <summary>جمع جعبه‌های وزن‌کشی‌شده (BoxFinalInfos).</summary>
        public decimal FinalizedBoxTotal { get; init; }

        /// <summary>جمع کالاها (محصولات + جعبه‌های نهایی‌شده).</summary>
        public decimal GoodsTotal => ProductTotal + FinalizedBoxTotal;

        /// <summary>هزینه پیک ثبت‌شده روی سفارش.</summary>
        public decimal StoredDeliveryFee { get; init; }

        /// <summary>هزینه پیک مؤثر پس از اعمال سیاست «ارسال رایگان از مبلغ مشخص».</summary>
        public decimal DeliveryFee { get; init; }

        /// <summary>تخفیف کد تخفیف (محدود به مبلغ کالا + پیک).</summary>
        public decimal DiscountAmount { get; init; }

        /// <summary>مبلغ کل قابل دریافت برای این سفارش تا این لحظه.</summary>
        public decimal GrandTotal { get; init; }

        /// <summary>مجموع پرداخت‌های انجام‌شده: درگاه موفق + اعتبار کیف پول مصرف‌شده.</summary>
        public decimal PaidTotal { get; init; }

        /// <summary>مبلغ باقی‌مانده برای پرداخت (هرگز منفی نمی‌شود).</summary>
        public decimal PayableNow { get; init; }

        /// <summary>آیا هنوز جعبه‌ای هست که وزن/قیمت نهایی‌اش ثبت نشده است؟</summary>
        public bool HasUnfinalizedBoxes { get; init; }

        /// <summary>جمع وزن نهایی جعبه‌های ثبت‌شده (گرم).</summary>
        public int FinalizedBoxWeightGrams { get; init; }

        /// <summary>آیا هزینه پیک به دلیل عبور از آستانه ارسال رایگان صفر شده است؟</summary>
        public bool IsDeliveryFree => StoredDeliveryFee > 0 && DeliveryFee == 0;
    }

    /// <summary>
    /// تنها منبع محاسبه مبلغ سفارش در برنامه.
    /// پیش از این، هر بخش (پرداخت، پنل ادمین، پروفایل مشتری، فاکتور) نسخه خودش را حساب می‌کرد
    /// و همین باعث می‌شد مبلغی که مشتری می‌دید با مبلغی که از درگاه دریافت می‌شد مغایرت داشته باشد.
    /// </summary>
    public class OrderPricingService
    {
        private readonly SugarShopSalesDbContext _salesDb;
        private decimal? _freeDeliveryThreshold;

        public OrderPricingService(SugarShopSalesDbContext salesDb)
        {
            _salesDb = salesDb;
        }

        /// <summary>
        /// محاسبه کامل وضعیت مالی سفارش.
        /// سفارش باید با <c>Include(o =&gt; o.Items)</c> بارگذاری شده باشد.
        /// </summary>
        /// <param name="includePayments">
        /// اگر فقط مبلغ سفارش لازم است (مثلاً لیست سفارش‌های مشتری)، با false دو کوئری جمع پرداخت‌ها حذف می‌شود.
        /// </param>
        public async Task<OrderPricing> ComputeAsync(Order order, bool includePayments = true)
        {
            var items = order.Items ?? new List<OrderItem>();

            decimal productTotal = 0;
            decimal finalizedBoxTotal = 0;
            int finalizedBoxWeight = 0;
            bool hasUnfinalizedBoxes = false;

            if (items.Count == 0)
            {
                // سفارش موقتِ تک‌مرحله‌ای (کیک سفارشی / شارژ کیف پول): ردیف فروش ندارد و
                // مبلغ کالا در TotalAmountSnapshot نگه داشته شده است.
                productTotal = order.TotalAmountSnapshot;
            }
            else
            {
                productTotal = items
                    .Where(i => i.ItemType == OrderItemType.Product)
                    .Sum(i => i.TotalPriceSnapshot);

                var boxTitles = items
                    .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                    .Select(i => i.BoxTitle!)
                    .Distinct()
                    .ToList();

                if (boxTitles.Count > 0)
                {
                    var boxInfos = await _salesDb.BoxFinalInfos.AsNoTracking()
                        .Where(b => b.OrderId == order.Id && boxTitles.Contains(b.BoxTitle))
                        .ToListAsync();

                    // اگر ردیف تکراری برای یک جعبه ثبت شده باشد، فقط جدیدترین ردیف حساب می‌شود
                    var latestPerBox = boxInfos
                        .GroupBy(b => b.BoxTitle)
                        .Select(g => g.OrderByDescending(b => b.UpdatedAt).ThenByDescending(b => b.Id).First())
                        .ToList();

                    finalizedBoxTotal = latestPerBox.Sum(b => b.FinalPrice);
                    finalizedBoxWeight = latestPerBox.Sum(b => b.FinalWeightGrams);
                    hasUnfinalizedBoxes = boxTitles.Any(t => latestPerBox.All(b => b.BoxTitle != t));
                }
            }

            var goodsTotal = productTotal + finalizedBoxTotal;

            // سیاست ارسال رایگان: عبور از آستانه، هزینه پیک را صفر می‌کند (آستانه ۰ = غیرفعال)
            var threshold = await GetFreeDeliveryThresholdAsync();
            var deliveryFee = order.DeliveryFeeSnapshot;
            if (threshold > 0 && goodsTotal >= threshold)
                deliveryFee = 0;

            var discount = Math.Min(order.DiscountAmountSnapshot ?? 0, goodsTotal + deliveryFee);
            if (discount < 0) discount = 0;

            var grandTotal = goodsTotal + deliveryFee - discount;
            if (grandTotal < 0) grandTotal = 0;

            decimal paidTotal = 0;
            if (includePayments)
            {
                var gatewayPaid = await _salesDb.Payments.AsNoTracking()
                    .Where(p => p.OrderId == order.Id && p.PaymentStatus == PaymentStatus.Succeeded)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                var walletPaid = await _salesDb.WalletTransactions.AsNoTracking()
                    .Where(t => t.OrderId == order.Id && t.Type == "Purchase" && t.Amount < 0)
                    .SumAsync(t => (decimal?)(-t.Amount)) ?? 0m;

                paidTotal = gatewayPaid + walletPaid;
            }

            return new OrderPricing
            {
                ProductTotal = productTotal,
                FinalizedBoxTotal = finalizedBoxTotal,
                StoredDeliveryFee = order.DeliveryFeeSnapshot,
                DeliveryFee = deliveryFee,
                DiscountAmount = discount,
                GrandTotal = grandTotal,
                PaidTotal = paidTotal,
                PayableNow = Math.Max(0, grandTotal - paidTotal),
                HasUnfinalizedBoxes = hasUnfinalizedBoxes,
                FinalizedBoxWeightGrams = finalizedBoxWeight
            };
        }

        /// <summary>
        /// همان محاسبه، به‌علاوه نوشتن مبلغ نهایی/وزن نهایی روی سفارش (بدون SaveChanges).
        /// پس از هر تغییری که مبلغ سفارش را جابه‌جا می‌کند (ثبت وزن جعبه، تغییر هزینه پیک، حذف ردیف)
        /// باید فراخوانی شود تا مبلغ نمایش‌داده‌شده همیشه با مبلغی که دریافت می‌شود یکی باشد.
        /// </summary>
        public async Task<OrderPricing> ApplyToOrderAsync(Order order)
        {
            var pricing = await ComputeAsync(order);

            order.FinalTotalAmount = pricing.GrandTotal;
            if (pricing.FinalizedBoxWeightGrams > 0)
                order.FinalTotalWeightGrams = pricing.FinalizedBoxWeightGrams;

            return pricing;
        }

        private async Task<decimal> GetFreeDeliveryThresholdAsync()
        {
            if (_freeDeliveryThreshold.HasValue) return _freeDeliveryThreshold.Value;

            _freeDeliveryThreshold = await _salesDb.SiteSettings.AsNoTracking()
                .Select(s => (decimal?)s.FreeDeliveryThreshold)
                .FirstOrDefaultAsync() ?? 0m;

            return _freeDeliveryThreshold.Value;
        }
    }
}
