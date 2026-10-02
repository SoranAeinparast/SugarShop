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
            var boxInfos = (await LoadBoxInfosAsync(new[] { order })).GetValueOrDefault(order.Id)
                ?? new List<BoxFinalInfo>();
            var threshold = await GetFreeDeliveryThresholdAsync();

            decimal paidTotal = 0;
            if (includePayments)
                paidTotal = (await LoadPaidTotalsAsync(new[] { order.Id })).GetValueOrDefault(order.Id);

            return BuildPricing(order, boxInfos, threshold, paidTotal);
        }

        /// <summary>
        /// محاسبه مبلغ چند سفارش با یک کوئری (بدون N+1) — برای لیست‌های صفحه‌بندی‌شده.
        /// اعداد دقیقاً از همان ریاضیِ <see cref="ComputeAsync"/> می‌آیند، پس مبلغ نمایش‌داده‌شده
        /// در لیست هرگز با مبلغی که از درگاه گرفته می‌شود مغایرت ندارد.
        /// سفارش‌ها باید با <c>Include(o =&gt; o.Items)</c> بارگذاری شده باشند.
        /// </summary>
        /// <param name="includePayments">
        /// اگر مبلغ باقی‌مانده (PayableNow) لازم باشد true بدهید؛ با false دو کوئری جمع پرداخت‌ها حذف می‌شود.
        /// </param>
        public async Task<Dictionary<int, OrderPricing>> ComputeManyAsync(
            IReadOnlyCollection<Order> orders,
            bool includePayments = false)
        {
            var result = new Dictionary<int, OrderPricing>();
            if (orders == null || orders.Count == 0) return result;

            // ۱) وزن/قیمت نهایی جعبه‌های همه سفارش‌های این صفحه: یک کوئری
            var boxInfos = await LoadBoxInfosAsync(orders);
            // ۲) آستانه ارسال رایگان: یک کوئری (و بعد از آن کش می‌شود)
            var threshold = await GetFreeDeliveryThresholdAsync();
            // ۳) جمع پرداخت‌های قبلی (فقط اگر لازم باشد): دو کوئری تجمیعی برای کل صفحه
            var paidTotals = includePayments
                ? await LoadPaidTotalsAsync(orders.Select(o => o.Id).ToList())
                : new Dictionary<int, decimal>();

            foreach (var order in orders)
            {
                result[order.Id] = BuildPricing(
                    order,
                    boxInfos.GetValueOrDefault(order.Id) ?? new List<BoxFinalInfo>(),
                    threshold,
                    paidTotals.GetValueOrDefault(order.Id));
            }

            return result;
        }

        /// <summary>
        /// ریاضیِ خالص مبلغ سفارش — تک‌منبع حقیقت برای همه مسیرها (تک‌سفارشی و گروهی).
        /// ورودی‌ها از قبل خوانده شده‌اند تا هیچ کوئری‌ای داخل محاسبه انجام نشود.
        /// </summary>
        private static OrderPricing BuildPricing(
            Order order,
            IReadOnlyList<BoxFinalInfo> boxInfosOfOrder,
            decimal freeDeliveryThreshold,
            decimal paidTotal)
        {
            var items = order.Items ?? new List<OrderItem>();

            decimal productTotal;
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
                    // اگر ردیف تکراری برای یک جعبه ثبت شده باشد، فقط جدیدترین ردیف حساب می‌شود
                    var latestPerBox = boxInfosOfOrder
                        .Where(b => boxTitles.Contains(b.BoxTitle))
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
            var deliveryFee = order.DeliveryFeeSnapshot;
            if (freeDeliveryThreshold > 0 && goodsTotal >= freeDeliveryThreshold)
                deliveryFee = 0;

            var discount = Math.Min(order.DiscountAmountSnapshot ?? 0, goodsTotal + deliveryFee);
            if (discount < 0) discount = 0;

            var grandTotal = goodsTotal + deliveryFee - discount;
            if (grandTotal < 0) grandTotal = 0;

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

        /// <summary>وزن/قیمت نهایی جعبه‌ها برای مجموعه‌ای از سفارش‌ها با یک کوئری (کلید = شناسه سفارش).</summary>
        private async Task<Dictionary<int, List<BoxFinalInfo>>> LoadBoxInfosAsync(IEnumerable<Order> orders)
        {
            var result = new Dictionary<int, List<BoxFinalInfo>>();

            var needing = orders
                .Where(o => (o.Items ?? new List<OrderItem>())
                    .Any(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle)))
                .Select(o => o.Id)
                .Distinct()
                .ToList();
            if (needing.Count == 0) return result;

            var rows = await _salesDb.BoxFinalInfos.AsNoTracking()
                .Where(b => needing.Contains(b.OrderId))
                .ToListAsync();

            foreach (var group in rows.GroupBy(b => b.OrderId))
                result[group.Key] = group.ToList();

            return result;
        }

        /// <summary>جمع پرداخت‌های انجام‌شده (درگاه موفق + اعتبار کیف پول) برای چند سفارش با دو کوئری تجمیعی.</summary>
        private async Task<Dictionary<int, decimal>> LoadPaidTotalsAsync(IReadOnlyCollection<int> orderIds)
        {
            var ids = orderIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, decimal>();

            var gateway = await _salesDb.Payments.AsNoTracking()
                .Where(p => ids.Contains(p.OrderId) && p.PaymentStatus == PaymentStatus.Succeeded)
                .GroupBy(p => p.OrderId)
                .Select(g => new { OrderId = g.Key, Total = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.OrderId, x => x.Total);

            var wallet = await _salesDb.WalletTransactions.AsNoTracking()
                .Where(t => t.OrderId != null && ids.Contains(t.OrderId.Value) && t.Type == "Purchase" && t.Amount < 0)
                .GroupBy(t => t.OrderId!.Value)
                .Select(g => new { OrderId = g.Key, Total = g.Sum(t => -t.Amount) })
                .ToDictionaryAsync(x => x.OrderId, x => x.Total);

            var result = new Dictionary<int, decimal>();
            foreach (var id in ids)
            {
                result[id] = gateway.GetValueOrDefault(id) + wallet.GetValueOrDefault(id);
            }

            return result;
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
