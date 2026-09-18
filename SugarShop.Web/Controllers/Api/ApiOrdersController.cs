using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using System.Security.Claims;

namespace SugarShop.Web.Controllers.Api
{
    /// <summary>
    /// سفارش‌های کاربر جاری — فقط سفارش‌های خودِ کاربرِ احرازشده.
    /// داده همان سفارش‌های سایت است؛ مشاهده در اپ و سایت کاملاً یکسان است.
    /// </summary>
    [ApiController]
    [Authorize(AuthenticationSchemes = "ApiJwt")]
    [Route("api/v1/orders")]
    public class ApiOrdersController : ControllerBase
    {
        private readonly SugarShopSalesDbContext _sales;
        private readonly SugarShop.Infrastructure.Persistence.SugarShopCatalogDbContext _catalog;

        public ApiOrdersController(SugarShopSalesDbContext sales, SugarShop.Infrastructure.Persistence.SugarShopCatalogDbContext catalog)
        {
            _sales = sales;
            _catalog = catalog;
        }

        private string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;

        /// <summary>فهرست سفارش‌های من</summary>
        [HttpGet]
        public async Task<IActionResult> MyOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var q = _sales.Orders.AsNoTracking()
                .Include(o => o.Items)
                .Where(o => o.UserId == userId
                    && o.Notes != "WalletRecharge"
                    && (o.Notes == null || !o.Notes.StartsWith("CustomCakeOrder_")))
                .OrderByDescending(o => o.CreatedAt);

            var total = await q.CountAsync();
            var orders = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var data = orders.Select(o => new
            {
                o.Id,
                o.OrderCode,
                o.CreatedAt,
                status = o.OrderStatus.ToString(),
                paymentStatus = o.PaymentStatus.ToString(),
                total = o.FinalTotalAmount > 0
                    ? o.FinalTotalAmount
                    : o.Items.Where(i => i.ItemType == OrderItemType.Product).Sum(i => i.TotalPriceSnapshot),
                itemsCount = o.Items.Count
            });

            return Ok(new { success = true, page, pageSize, total, data });
        }

        /// <summary>جزئیات یک سفارش از سفارش‌های من</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _sales.Orders.AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
            if (order == null) return NotFound(new { success = false, message = "سفارش یافت نشد." });

            var productIds = order.Items.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();
            var names = productIds.Count > 0
                ? await _catalog.Products.AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.TitleFa)
                : new Dictionary<int, string>();

            var boxInfos = await _sales.BoxFinalInfos.AsNoTracking()
                .Where(b => b.OrderId == id)
                .ToListAsync();

            // وزن واقعی هر ردیف جعبه (وزن‌کشی فروشگاه) هم به اپ داده می‌شود
            var sweetIds = order.Items.Where(x => x.SweetItemId.HasValue).Select(x => x.SweetItemId!.Value).Distinct().ToList();
            var sweetNames = sweetIds.Count > 0
                ? await _catalog.SweetItems.AsNoTracking()
                    .Where(s => sweetIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.TitleFa)
                : new Dictionary<int, string>();

            var data = new
            {
                order.Id,
                order.OrderCode,
                order.CreatedAt,
                status = order.OrderStatus.ToString(),
                paymentStatus = order.PaymentStatus.ToString(),
                order.CustomerName,
                order.CustomerFullAddress,
                order.DeliveryDate,
                order.DeliveryTime,
                order.DeliveryFeeSnapshot,
                order.DiscountAmountSnapshot,
                products = order.Items
                    .Where(i => i.ItemType == OrderItemType.Product)
                    .Select(i => new { name = i.ProductId.HasValue && names.ContainsKey(i.ProductId.Value) ? names[i.ProductId.Value] : "محصول", i.Quantity, i.UnitPriceSnapshot, i.TotalPriceSnapshot }),
                boxes = boxInfos.Select(b => new { b.BoxTitle, b.FinalWeightGrams, b.FinalPrice }),
                boxRows = order.Items
                    .Where(i => i.ItemType == OrderItemType.SweetItem && !string.IsNullOrEmpty(i.BoxTitle))
                    .Select(i => new
                    {
                        boxTitle = i.BoxTitle,
                        name = i.SweetItemId.HasValue && sweetNames.ContainsKey(i.SweetItemId.Value) ? sweetNames[i.SweetItemId.Value] : "شیرینی",
                        weightGrams = i.WeightSnapshotGrams ?? 0,
                        i.Quantity,
                        price = i.TotalPriceSnapshot
                    })
            };

            return Ok(new { success = true, data });
        }
    }
}
