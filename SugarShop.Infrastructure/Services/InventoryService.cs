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

        public async Task DecreaseInventoryAsync(Order order)
        {
            if (order.Items == null || !order.Items.Any())
                return;

            var productIds = order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId.Value).ToList();
            var sweetIds = order.Items.Where(i => i.SweetItemId.HasValue).Select(i => i.SweetItemId.Value).ToList();

            if (productIds.Any())
            {
                var products = await _catalogDb.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);
                foreach (var item in order.Items.Where(i => i.ProductId.HasValue))
                {
                    if (products.TryGetValue(item.ProductId.Value, out var product))
                    {
                        product.Inventory -= item.Quantity;
                        if (product.Inventory < 0) product.Inventory = 0;
                    }
                }
            }

            if (sweetIds.Any())
            {
                var sweets = await _catalogDb.SweetItems
                    .Where(s => sweetIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id);
                foreach (var item in order.Items.Where(i => i.SweetItemId.HasValue))
                {
                    if (sweets.TryGetValue(item.SweetItemId.Value, out var sweet))
                    {
                        sweet.InventoryCount -= item.Quantity;
                        if (sweet.InventoryCount < 0) sweet.InventoryCount = 0;
                    }
                }
            }

            await _catalogDb.SaveChangesAsync();
        }
    }
}