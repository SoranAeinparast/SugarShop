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
        public async Task DecreaseInventoryAsync(Order order)
        {
            if (order.Items == null || !order.Items.Any())
                return;

            foreach (var item in order.Items.Where(i => i.ProductId.HasValue && i.Quantity > 0))
            {
                var quantity = item.Quantity;
                await _catalogDb.Products
                    .Where(p => p.Id == item.ProductId!.Value)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        p => p.Inventory,
                        p => p.Inventory >= quantity ? p.Inventory - quantity : 0));
            }

            foreach (var item in order.Items.Where(i => i.SweetItemId.HasValue && i.Quantity > 0))
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