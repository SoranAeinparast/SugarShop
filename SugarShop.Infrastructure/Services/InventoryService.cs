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
        /// موجودی را فقط وقتی به‌صورت اتمیک کم می‌کند که تمام ردیف‌های سفارش موجودی کافی داشته باشند.
        /// فراخواننده باید هنگام پردازش سفارش این کار را در تراکنش قرار دهد تا در کمبود یک ردیف،
        /// کسرهای قبلی هم rollback شوند.
        /// </summary>
        public async Task<bool> TryDecreaseInventoryAsync(IEnumerable<OrderItem>? items)
        {
            if (items == null) return true;

            var rows = items.ToList();
            if (rows.Any(i => i.Quantity < 1)) return false;

            var products = rows.Where(i => i.ProductId.HasValue)
                .GroupBy(i => i.ProductId!.Value)
                .Select(g => new { Id = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .OrderBy(x => x.Id);
            foreach (var item in products)
            {
                var changed = await _catalogDb.Products
                    .Where(p => p.Id == item.Id && p.Inventory >= item.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Inventory, p => p.Inventory - item.Quantity));
                if (changed != 1) return false;
            }

            var sweets = rows.Where(i => i.SweetItemId.HasValue)
                .GroupBy(i => i.SweetItemId!.Value)
                .Select(g => new { Id = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .OrderBy(x => x.Id);
            foreach (var item in sweets)
            {
                var changed = await _catalogDb.SweetItems
                    .Where(s => s.Id == item.Id && s.InventoryCount >= item.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(s => s.InventoryCount, s => s.InventoryCount - item.Quantity));
                if (changed != 1) return false;
            }

            return true;
        }
    }
}
