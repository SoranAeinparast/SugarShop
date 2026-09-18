using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers.Api
{
    /// <summary>
    /// کاتالوگ عمومی — داده‌ها مستقیم از همان دیتابیس سایت می‌آید؛
    /// هر تغییری در پنل ادمین بلافاصله در اپ هم دیده می‌شود.
    /// </summary>
    [ApiController]
    [Route("api/v1/catalog")]
    public class ApiCatalogController : ControllerBase
    {
        private readonly SugarShopCatalogDbContext _catalog;
        private readonly SugarShopSalesDbContext _sales;

        public ApiCatalogController(SugarShopCatalogDbContext catalog, SugarShopSalesDbContext sales)
        {
            _catalog = catalog;
            _sales = sales;
        }
        // SiteSettings روی SalesDbContext است

        /// <summary>فهرست دسته‌بندی‌های فعال</summary>
        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> Categories()
        {
            var list = await _catalog.Categories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new { c.Id, c.TitleFa, c.Slug, c.ImagePath, c.RequiresBoxSelection })
                .ToListAsync();
            return Ok(new { success = true, data = list });
        }

        /// <summary>محصولات قیمت‌ثابت (اختیاری: بر اساس دسته)</summary>
        [HttpGet("products")]
        [AllowAnonymous]
        public async Task<IActionResult> Products([FromQuery] int? categoryId = null)
        {
            var q = _catalog.Products.AsNoTracking().Where(p => p.IsActive);
            if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);

            var list = await q.OrderBy(p => p.SortOrder)
                .Select(p => new
                {
                    p.Id,
                    p.TitleFa,
                    p.Slug,
                    p.Price,
                    p.WeightGrams,
                    p.ImagePath,
                    p.ShortDescription,
                    p.Inventory,
                    p.CategoryId
                })
                .ToListAsync();
            return Ok(new { success = true, data = list });
        }

        /// <summary>شیرینی‌های انتخابی جعبه (قیمت به کیلوگرم)</summary>
        [HttpGet("sweets")]
        [AllowAnonymous]
        public async Task<IActionResult> Sweets()
        {
            var list = await _catalog.SweetItems.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => new
                {
                    s.Id,
                    s.TitleFa,
                    s.ImagePath,
                    s.PricePerKg,
                    s.ApproxWeightGrams,
                    s.InventoryCount,
                    s.CategoryId
                })
                .ToListAsync();
            return Ok(new { success = true, data = list });
        }

        /// <summary>جعبه‌های قابل انتخاب</summary>
        [HttpGet("boxes")]
        [AllowAnonymous]
        public async Task<IActionResult> Boxes()
        {
            var list = await _catalog.BoxTypes.AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.SortOrder)
                .Select(b => new { b.Id, b.TitleFa, b.CapacityGrams, b.MaxRows })
                .ToListAsync();
            return Ok(new { success = true, data = list });
        }

        /// <summary>اسلایدرهای صفحه اصلی</summary>
        [HttpGet("sliders")]
        [AllowAnonymous]
        public async Task<IActionResult> Sliders()
        {
            var now = DateTime.UtcNow;
            var list = await _catalog.Sliders.AsNoTracking()
                .Where(s => s.IsActive
                    && (s.StartAt == null || s.StartAt <= now)
                    && (s.EndAt == null || s.EndAt >= now))
                .OrderBy(s => s.SortOrder)
                .Select(s => new { s.Id, s.Title, s.Subtitle, s.ImagePath, s.IsVideo, s.ButtonText, s.ButtonUrl })
                .ToListAsync();
            return Ok(new { success = true, data = list });
        }

        /// <summary>اطلاعات کلی فروشگاه (تماس/شبکه‌های اجتماعی/آدرس)</summary>
        [HttpGet("site-info")]
        [AllowAnonymous]
        public async Task<IActionResult> SiteInfo()
        {
            var s = await _sales.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            if (s == null) return Ok(new { success = true, data = new { } });
            return Ok(new
            {
                success = true,
                data = new
                {
                    s.SiteTitle,
                    s.SiteDescription,
                    s.LogoPath,
                    s.Phone,
                    s.Email,
                    s.Address,
                    s.WorkingHours,
                    s.InstagramUrl,
                    s.TelegramUrl,
                    s.WhatsAppUrl
                }
            });
        }

        /// <summary>آدرس‌های کاربر جاری (نیازمند توکن)</summary>
        // اسکیم باید صریحاً ApiJwt باشد؛ در غیر این صورت [Authorize] پیش‌فرض روی کوکی Identity
        // اعتبارسنجی می‌کند و توکن Bearer اپلیکیشن موبایل هرگز پذیرفته نمیشد.
        [HttpGet("my-addresses")]
        [Authorize(AuthenticationSchemes = "ApiJwt")]
        public async Task<IActionResult> MyAddresses()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var list = await _sales.Addresses.AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .Select(a => new { a.Id, a.Title, a.FullAddress, a.PostalCode, a.ReceiverName, a.ReceiverPhone, a.IsDefault })
                .ToListAsync();
            return Ok(new { success = true, data = list });
        }
    }
}
