using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// صدور/دریافت/بازتولید فاکتور برای یک سفارش.
    /// ردیف‌های جعبه به‌صورت «یک ردیف به ازای هر جعبه» با قیمت/وزن نهایی (در صورت ثبت) گروه‌بندی می‌شوند
    /// و محصولات عادی هرکدام یک ردیف جداگانه دارند. مبلغ‌ها به تومان ذخیره می‌شوند (نمایش ریال در ویو انجام می‌شود).
    /// </summary>
    public static class InvoiceService
    {
        public static async Task<Invoice> GetOrCreateForOrderAsync(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            int orderId)
        {
            var existing = await salesDb.Invoices
                .Include(i => i.Items)
                .Include(i => i.Order)
                .FirstOrDefaultAsync(i => i.OrderId == orderId);
            if (existing != null) return existing;

            var order = await salesDb.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId)
                ?? throw new InvalidOperationException("OrderNotFound");

            var invoice = new Invoice
            {
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                OrderId = order.Id,
                InvoiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await PopulateInvoiceAsync(salesDb, catalogDb, invoice);

            salesDb.Invoices.Add(invoice);
            await salesDb.SaveChangesAsync();
            return invoice;
        }

        /// <summary>
        /// بازتولید فاکتور موجود پس از تغییر ردیف‌ها/مبالغ سفارش توسط ادمین.
        /// شماره و تاریخ صدور فاکتور حفظ می‌شود و فقط اقلام و مبالغ از روی وضعیت فعلی سفارش بازسازی می‌شود.
        /// اگر فاکتوری برای سفارش وجود نداشته باشد، کاری انجام نمی‌دهد.
        /// </summary>
        public static async Task RegenerateIfExistsAsync(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            int orderId)
        {
            var invoice = await salesDb.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.OrderId == orderId);
            if (invoice == null) return;

            salesDb.InvoiceItems.RemoveRange(invoice.Items);
            invoice.Items.Clear();

            await PopulateInvoiceAsync(salesDb, catalogDb, invoice);
            invoice.UpdatedAt = DateTime.UtcNow;
            await salesDb.SaveChangesAsync();
        }

        private static async Task PopulateInvoiceAsync(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            Invoice invoice)
        {
            var order = await salesDb.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == invoice.OrderId)
                ?? throw new InvalidOperationException("OrderNotFound");

            // نام کالاها از کاتالوگ
            var sweetIds = order.Items.Where(x => x.SweetItemId.HasValue).Select(x => x.SweetItemId!.Value).Distinct().ToList();
            var productIds = order.Items.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();

            var sweetNames = sweetIds.Any()
                ? await catalogDb.SweetItems.Where(s => sweetIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.TitleFa)
                : new Dictionary<int, string>();
            var productNames = productIds.Any()
                ? await catalogDb.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.TitleFa)
                : new Dictionary<int, string>();

            var boxFinals = await salesDb.BoxFinalInfos.Where(b => b.OrderId == order.Id).ToListAsync();

            // هر جعبه → یک ردیف فاکتور (با وزن/قیمت نهایی در صورت ثبت)
            var boxGroups = order.Items
                .Where(i => i.ItemType == OrderItemType.SweetItem)
                .GroupBy(i => string.IsNullOrEmpty(i.BoxTitle) ? "جعبه شیرینی" : i.BoxTitle);

            foreach (var group in boxGroups)
            {
                var members = group.ToList();
                var boxFinal = boxFinals.FirstOrDefault(b => b.BoxTitle == group.Key);

                var weightGrams = boxFinal?.FinalWeightGrams
                    ?? members.Sum(m => m.WeightSnapshotGrams ?? 0);
                var totalPrice = boxFinal?.FinalPrice
                    ?? members.Sum(m => m.TotalPriceSnapshot);

                var details = string.Join("، ", members.Select(m =>
                    $"{(m.SweetItemId.HasValue && sweetNames.ContainsKey(m.SweetItemId.Value) ? sweetNames[m.SweetItemId.Value] : "شیرینی")} ({m.Quantity})"));

                invoice.Items.Add(new InvoiceItem
                {
                    ItemName = $"{group.Key}\n{details}",
                    Quantity = 1,
                    UnitPrice = totalPrice,
                    TotalPrice = totalPrice,
                    WeightGrams = weightGrams,
                    BoxTitle = group.Key
                });
            }

            // محصولات عادی
            foreach (var item in order.Items.Where(i => i.ItemType == OrderItemType.Product))
            {
                var name = item.ProductId.HasValue && productNames.ContainsKey(item.ProductId.Value)
                    ? productNames[item.ProductId.Value]
                    : "محصول";
                invoice.Items.Add(new InvoiceItem
                {
                    ItemName = name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPriceSnapshot,
                    TotalPrice = item.TotalPriceSnapshot,
                    WeightGrams = item.WeightSnapshotGrams,
                    BoxTitle = null
                });
            }

            // محاسبه مبلغ‌ها: جمع اقلام − تخفیف + پیک + مالیات
            var subtotal = invoice.Items.Sum(i => i.TotalPrice);
            var discount = Math.Min(order.DiscountAmountSnapshot ?? 0, subtotal);
            var total = subtotal - discount + order.DeliveryFeeSnapshot + (order.TaxAmountSnapshot ?? 0);
            if (total < 0) total = 0;

            invoice.Subtotal = subtotal;
            invoice.DiscountAmount = discount;
            invoice.DeliveryFee = order.DeliveryFeeSnapshot;
            invoice.TaxAmount = order.TaxAmountSnapshot ?? 0;
            invoice.TotalAmount = total;
            invoice.Notes = order.AdminNotes;
        }
    }
}