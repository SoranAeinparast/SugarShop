using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// 💰 قفل‌کردن ریاضیات پول.
///
/// «مبلغ سفارش» پرخطرترین منطق برنامه است: هر جابه‌جایی در آن یعنی اختلاف بین عددی که مشتری
/// می‌بیند و عددی که از درگاه گرفته می‌شود. این آزمون‌ها همه‌ی حالت‌های واقعی را روی
/// <see cref="OrderPricingService"/> (تنها منبع محاسبه‌ی مبلغ) ثابت می‌کنند و به هیچ دیتابیسی وصل نیستند.
/// </summary>
public class OrderPricingServiceTests
{
    private static SugarShopSalesDbContext NewDb()
        => new(new DbContextOptionsBuilder<SugarShopSalesDbContext>()
            .UseInMemoryDatabase("pricing-" + Guid.NewGuid().ToString("N"))
            .Options);

    /// <summary>سفارش فروشگاهی با محصولات قیمت‌ثابت و جعبه‌های شیرینیِ وزن‌کشی‌شده.</summary>
    private static Order BuildOrder(
        decimal productTotal = 0,
        decimal deliveryFee = 0,
        decimal? discount = null,
        string[]? boxTitles = null)
    {
        var order = new Order
        {
            OrderCode = "T-1",
            OrderStatus = OrderStatus.PendingPayment,
            PaymentStatus = PaymentStatus.Unpaid,
            TotalAmountSnapshot = productTotal,
            DeliveryFeeSnapshot = deliveryFee,
            DiscountAmountSnapshot = discount
        };

        if (productTotal > 0)
        {
            order.Items.Add(new OrderItem
            {
                ItemType = OrderItemType.Product,
                Quantity = 1,
                UnitPriceSnapshot = productTotal,
                TotalPriceSnapshot = productTotal
            });
        }

        foreach (var title in boxTitles ?? Array.Empty<string>())
        {
            order.Items.Add(new OrderItem
            {
                ItemType = OrderItemType.SweetItem,
                BoxTitle = title,
                Quantity = 1,
                TotalPriceSnapshot = 0
            });
        }

        return order;
    }

    private static Payment SucceededPayment(int orderId, decimal amount, string code = "TX-1")
        => new()
        {
            OrderId = orderId,
            Amount = amount,
            PaymentStatus = PaymentStatus.Succeeded,
            TransactionCode = code
        };

    /// <summary>سفارش و وزن‌کشی جعبه‌هایش را ذخیره می‌کند (AgeMinutes = چند دقیقه پیش ثبت شده).</summary>
    private static async Task SaveAsync(
        SugarShopSalesDbContext db,
        Order order,
        params (string Title, decimal Price, int Weight, int AgeMinutes)[] weightedBoxes)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        foreach (var box in weightedBoxes)
        {
            db.BoxFinalInfos.Add(new BoxFinalInfo
            {
                OrderId = order.Id,
                BoxTitle = box.Title,
                FinalPrice = box.Price,
                FinalWeightGrams = box.Weight,
                CreatedAt = now.AddMinutes(-box.AgeMinutes),
                UpdatedAt = now.AddMinutes(-box.AgeMinutes)
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Goods_delivery_and_discount_are_added_exactly_once()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 100_000m, deliveryFee: 30_000m, discount: 20_000m);
        await SaveAsync(db, order);

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(100_000m, pricing.ProductTotal);
        Assert.Equal(0m, pricing.FinalizedBoxTotal);
        Assert.Equal(100_000m, pricing.GoodsTotal);
        Assert.Equal(30_000m, pricing.DeliveryFee);
        Assert.Equal(20_000m, pricing.DiscountAmount);
        Assert.Equal(110_000m, pricing.GrandTotal);
        Assert.Equal(110_000m, pricing.PayableNow);
    }

    [Fact]
    public async Task Weighed_boxes_are_summed_with_their_exact_weight()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 50_000m, boxTitles: new[] { "جعبه یک", "جعبه دو" });
        await SaveAsync(db, order,
            ("جعبه یک", 320_000m, 1600, 30),
            ("جعبه دو", 180_000m, 900, 10));

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(500_000m, pricing.FinalizedBoxTotal);
        Assert.Equal(2500, pricing.FinalizedBoxWeightGrams);
        Assert.False(pricing.HasUnfinalizedBoxes);
        Assert.Equal(550_000m, pricing.GrandTotal);
    }

    [Fact]
    public async Task Box_title_repeated_in_several_rows_is_not_double_counted()
    {
        await using var db = NewDb();
        var order = BuildOrder(boxTitles: new[] { "جعبه یک" });
        await SaveAsync(db, order, ("جعبه یک", 100_000m, 500, 60));

        // ثبت دوباره‌ی وزن‌کشی همان جعبه (اصلاح وزن): فقط جدیدترین ردیف حساب می‌شود
        db.BoxFinalInfos.Add(new BoxFinalInfo
        {
            OrderId = order.Id,
            BoxTitle = "جعبه یک",
            FinalPrice = 120_000m,
            FinalWeightGrams = 600,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(120_000m, pricing.FinalizedBoxTotal);
        Assert.Equal(600, pricing.FinalizedBoxWeightGrams);
        Assert.Equal(120_000m, pricing.GrandTotal);
    }

    [Fact]
    public async Task Unweighed_boxes_are_flagged_and_kept_out_of_the_amount()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 40_000m, boxTitles: new[] { "جعبه یک", "جعبه دو" });
        await SaveAsync(db, order, ("جعبه یک", 200_000m, 1000, 5));

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(200_000m, pricing.FinalizedBoxTotal);
        Assert.True(pricing.HasUnfinalizedBoxes);
        Assert.Equal(240_000m, pricing.GrandTotal);
    }

    [Fact]
    public async Task Temporary_orders_without_items_use_the_snapshot_amount()
    {
        await using var db = NewDb();
        // سفارش‌های موقتِ تک‌مرحله‌ای (کیک سفارشی، شارژ کیف پول) ردیف فروش ندارند
        var order = BuildOrder(productTotal: 700_000m);
        order.Items.Clear();
        await SaveAsync(db, order);

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(700_000m, pricing.ProductTotal);
        Assert.Equal(700_000m, pricing.GrandTotal);
    }

    [Fact]
    public async Task Free_delivery_threshold_zeroes_the_fee_only_when_crossed()
    {
        await using var db = NewDb();
        db.SiteSettings.Add(new SiteSetting { FreeDeliveryThreshold = 500_000m });
        await db.SaveChangesAsync();

        var below = BuildOrder(productTotal: 499_999m, deliveryFee: 30_000m);
        var atThreshold = BuildOrder(productTotal: 500_000m, deliveryFee: 30_000m);
        await SaveAsync(db, below);
        await SaveAsync(db, atThreshold);

        var service = new OrderPricingService(db);
        var belowPricing = await service.ComputeAsync(below);
        var atThresholdPricing = await service.ComputeAsync(atThreshold);

        Assert.Equal(30_000m, belowPricing.DeliveryFee);
        Assert.Equal(529_999m, belowPricing.GrandTotal);
        Assert.False(belowPricing.IsDeliveryFree);

        Assert.Equal(0m, atThresholdPricing.DeliveryFee);
        Assert.Equal(500_000m, atThresholdPricing.GrandTotal);
        Assert.True(atThresholdPricing.IsDeliveryFree);
    }

    [Fact]
    public async Task Zero_threshold_means_free_delivery_is_disabled()
    {
        await using var db = NewDb();
        db.SiteSettings.Add(new SiteSetting { FreeDeliveryThreshold = 0m });
        await db.SaveChangesAsync();

        var order = BuildOrder(productTotal: 5_000_000m, deliveryFee: 30_000m);
        await SaveAsync(db, order);

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(30_000m, pricing.DeliveryFee);
        Assert.False(pricing.IsDeliveryFree);
    }

    [Fact]
    public async Task Discount_is_capped_and_the_total_never_goes_negative()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 100_000m, deliveryFee: 20_000m, discount: 999_999m);
        await SaveAsync(db, order);

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(120_000m, pricing.DiscountAmount);
        Assert.Equal(0m, pricing.GrandTotal);
        Assert.Equal(0m, pricing.PayableNow);
    }

    [Fact]
    public async Task Negative_discount_is_ignored()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 100_000m, discount: -50_000m);
        await SaveAsync(db, order);

        var pricing = await new OrderPricingService(db).ComputeAsync(order);

        Assert.Equal(0m, pricing.DiscountAmount);
        Assert.Equal(100_000m, pricing.GrandTotal);
    }

    [Fact]
    public async Task Previous_payments_are_subtracted_from_what_is_due_now()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 400_000m, deliveryFee: 30_000m);
        await SaveAsync(db, order);

        db.Payments.Add(SucceededPayment(order.Id, 200_000m));
        // پرداخت ناموفق نباید از بدهی کم کند
        db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Amount = 100_000m,
            PaymentStatus = PaymentStatus.Failed,
            TransactionCode = "TX-FAILED"
        });
        db.WalletTransactions.Add(new WalletTransaction
        {
            UserId = "u-1",
            OrderId = order.Id,
            Type = "Purchase",
            Amount = -50_000m
        });
        await db.SaveChangesAsync();

        var pricing = await new OrderPricingService(db).ComputeAsync(order, includePayments: true);

        Assert.Equal(430_000m, pricing.GrandTotal);
        Assert.Equal(250_000m, pricing.PaidTotal);   // ۲۰۰٬۰۰۰ درگاه موفق + ۵۰٬۰۰۰ کیف پول
        Assert.Equal(180_000m, pricing.PayableNow);
    }

    [Fact]
    public async Task Overpayment_never_produces_a_negative_payable()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 100_000m);
        await SaveAsync(db, order);

        db.WalletTransactions.Add(new WalletTransaction
        {
            UserId = "u-1",
            OrderId = order.Id,
            Type = "Purchase",
            Amount = -150_000m
        });
        await db.SaveChangesAsync();

        var pricing = await new OrderPricingService(db).ComputeAsync(order, includePayments: true);

        Assert.Equal(150_000m, pricing.PaidTotal);
        Assert.Equal(0m, pricing.PayableNow);
    }

    [Fact]
    public async Task Batch_result_is_identical_to_the_single_order_result()
    {
        // این برابری، ضامن یکسان بودن مبلغ در «لیست سفارش‌های من» (محاسبه‌ی گروهی) با
        // «صفحه‌ی پرداخت» (محاسبه‌ی تک‌سفارشی) است؛ همان چیزی که قبلاً با N+1 محاسبه می‌شد.
        await using var db = NewDb();
        db.SiteSettings.Add(new SiteSetting { FreeDeliveryThreshold = 300_000m });
        await db.SaveChangesAsync();

        var withBox = BuildOrder(productTotal: 250_000m, deliveryFee: 30_000m, boxTitles: new[] { "ج۱" });
        var freeDelivery = BuildOrder(productTotal: 500_000m, deliveryFee: 30_000m, discount: 50_000m);
        var pendingBox = BuildOrder(boxTitles: new[] { "ج۱" });

        await SaveAsync(db, withBox, ("ج۱", 120_000m, 700, 20));
        await SaveAsync(db, freeDelivery);
        await SaveAsync(db, pendingBox);

        var orders = new List<Order> { withBox, freeDelivery, pendingBox };
        foreach (var order in orders)
        {
            db.Payments.Add(SucceededPayment(order.Id, 10_000m));
        }
        await db.SaveChangesAsync();

        var service = new OrderPricingService(db);
        var batch = await service.ComputeManyAsync(orders, includePayments: true);

        foreach (var order in orders)
        {
            var single = await service.ComputeAsync(order, includePayments: true);
            var fromBatch = batch[order.Id];

            Assert.Equal(single.ProductTotal, fromBatch.ProductTotal);
            Assert.Equal(single.FinalizedBoxTotal, fromBatch.FinalizedBoxTotal);
            Assert.Equal(single.DeliveryFee, fromBatch.DeliveryFee);
            Assert.Equal(single.DiscountAmount, fromBatch.DiscountAmount);
            Assert.Equal(single.GrandTotal, fromBatch.GrandTotal);
            Assert.Equal(single.PaidTotal, fromBatch.PaidTotal);
            Assert.Equal(single.PayableNow, fromBatch.PayableNow);
            Assert.Equal(single.HasUnfinalizedBoxes, fromBatch.HasUnfinalizedBoxes);
            Assert.Equal(single.FinalizedBoxWeightGrams, fromBatch.FinalizedBoxWeightGrams);
        }
    }

    [Fact]
    public async Task Batch_without_payments_reports_no_paid_amount()
    {
        await using var db = NewDb();
        var order = BuildOrder(productTotal: 100_000m);
        await SaveAsync(db, order);
        db.Payments.Add(SucceededPayment(order.Id, 40_000m));
        await db.SaveChangesAsync();

        var service = new OrderPricingService(db);
        var withoutPayments = (await service.ComputeManyAsync(new[] { order }, includePayments: false))[order.Id];
        var withPayments = (await service.ComputeManyAsync(new[] { order }, includePayments: true))[order.Id];

        Assert.Equal(0m, withoutPayments.PaidTotal);
        Assert.Equal(100_000m, withoutPayments.PayableNow);
        Assert.Equal(40_000m, withPayments.PaidTotal);
        Assert.Equal(100_000m, withPayments.GrandTotal); // مبلغ کل با پرداخت‌ها تغییر نمی‌کند
    }

    [Fact]
    public async Task Empty_batch_is_safe()
    {
        await using var db = NewDb();
        var result = await new OrderPricingService(db).ComputeManyAsync(Array.Empty<Order>());
        Assert.Empty(result);
    }

    [Fact]
    public async Task Orders_in_the_same_batch_never_share_each_other_amounts()
    {
        await using var db = NewDb();
        var first = BuildOrder(productTotal: 100_000m, boxTitles: new[] { "ج" });
        var second = BuildOrder(productTotal: 70_000m, boxTitles: new[] { "ج" });
        await SaveAsync(db, first, ("ج", 200_000m, 1000, 5));
        await SaveAsync(db, second, ("ج", 10_000m, 100, 5));

        var batch = await new OrderPricingService(db).ComputeManyAsync(new[] { first, second });

        Assert.Equal(300_000m, batch[first.Id].GrandTotal);
        Assert.Equal(1000, batch[first.Id].FinalizedBoxWeightGrams);
        Assert.Equal(80_000m, batch[second.Id].GrandTotal);
        Assert.Equal(100, batch[second.Id].FinalizedBoxWeightGrams);
    }
}
