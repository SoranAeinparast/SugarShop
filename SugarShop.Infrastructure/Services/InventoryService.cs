using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Infrastructure.Services
{
    public class InventoryService
    {
        private readonly SugarShopCatalogDbContext _catalogDb;

        public InventoryService(SugarShopCatalogDbContext catalogDb)
        {
            _catalogDb = catalogDb;
        }

        /// <summary>
        /// کسر اتمیک موجودی انبار پس از موفقیت پرداخت.
        /// هر ردیف با یک UPDATE شرطی در سطح دیتابیس کسر می‌شود تا دو درخواست هم‌زمان
        /// نتوانند از یک موجودی واحد دو بار کسر کنند؛ در صورت کمبود موجودی، مقدار به صفر می‌رسد (منفی نمی‌شود).
        /// </summary>
        public Task DecreaseInventoryAsync(Order order)
            => DecreaseInventoryAsync(order?.Items);

        /// <summary>
        /// همان عملیات، ولی روی مجموعه‌ای از ردیف‌های سفارش. برای مواردی لازم است که ردیف‌ها
        /// در همان تراکنش ساخته شده‌اند و ناوبری Order.Items هنوز پر نشده است.
        /// </summary>
        public async Task DecreaseInventoryAsync(IEnumerable<OrderItem>? items)
        {
            if (items == null) return;

            var rows = items.Where(i => i.Quantity > 0).ToList();
            if (rows.Count == 0) return;

            foreach (var item in rows.Where(i => i.ProductId.HasValue))
            {
                var quantity = item.Quantity;
                await _catalogDb.Products
                    .Where(p => p.Id == item.ProductId!.Value)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        p => p.Inventory,
                        p => p.Inventory >= quantity ? p.Inventory - quantity : 0));
            }

            foreach (var item in rows.Where(i => i.SweetItemId.HasValue))
            {
                var quantity = item.Quantity;
                await _catalogDb.SweetItems
                    .Where(s => s.Id == item.SweetItemId!.Value)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        s => s.InventoryCount,
                        s => s.InventoryCount >= quantity ? s.InventoryCount - quantity : 0));
            }
        }
    }
}