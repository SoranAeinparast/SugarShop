using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.SessionModels;
using Microsoft.AspNetCore.Authorization;

namespace SugarShop.Web.Controllers
{
    public class CartController : Controller
    {
        /// <summary>سقف تعداد هر محصول در یک ردیف سبد خرید (محافظت از ورودی دستی/AJAX).</summary>
        private const int MaxQuantityPerProduct = 100;

        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly SugarShopSalesDbContext _salesDb;

        public CartController(SugarShopCatalogDbContext catalogDb, SugarShopSalesDbContext salesDb)
        {
            _catalogDb = catalogDb;
            _salesDb = salesDb;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProductToCart(int productId, int quantity = 1)
        {
            var product = await _catalogDb.Products.FindAsync(productId);
            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, title = "خطا", message = "محصول نامعتبر یا غیرفعال است.", type = "error" });
            }

            // 🔒 امنیت مالی: تعداد باید عدد صحیح مثبت باشد؛ تعداد منفی/صفر باعث معکوس شدن
            // محاسبات قیمت و در نتیجه بستانکار شدن اشتباه کیف پول می‌شد.
            if (quantity < 1)
            {
                return Json(new { success = false, title = "خطا", message = "تعداد انتخاب‌شده معتبر نیست.", type = "error" });
            }
            if (quantity > MaxQuantityPerProduct) quantity = MaxQuantityPerProduct;

            var cartState = HttpContext.Session.GetCartSessionState();
            var existing = cartState.Products.FirstOrDefault(p => p.ProductId == productId);

            // ردیف‌های معیوبِ باقی‌مانده از نشست‌های قبلی (تعداد صفر/منفی) پاک‌سازی می‌شوند
            if (existing != null && existing.Quantity < 1)
            {
                existing.Quantity = 0;
            }

            // موجودی بر اساس مجموع ردیف (موجود + درخواست جدید) بررسی می‌شود، نه فقط درخواست جدید
            int resultingQuantity = (existing?.Quantity ?? 0) + quantity;
            if (resultingQuantity > product.Inventory)
            {
                return Json(new
                {
                    success = false,
                    title = "ناموجود",
                    message = $"متأسفانه موجودی {product.TitleFa} کافی نیست. موجودی فعلی: {product.Inventory}",
                    type = "warning"
                });
            }

            if (existing != null)
                existing.Quantity = resultingQuantity;
            else
            {
                cartState.Products.Add(new CartProductItem
                {
                    ProductId = product.Id,
                    Title = product.TitleFa,
                    Price = product.Price,
                    WeightGrams = (int)(product.WeightGrams ?? 0),
                    Quantity = quantity
                });
            }
            HttpContext.Session.SetCartSessionState(cartState);

            return Json(new { success = true, title = "افزوده شد", message = $"{product.TitleFa} با موفقیت به سبد خرید اضافه شد.", type = "success" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveProduct(int productId)
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            cartState.RemoveProduct(productId);
            HttpContext.Session.SetCartSessionState(cartState);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Index()
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            return View(cartState);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveItem(string id)
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            cartState.RemoveItem(id);
            HttpContext.Session.SetCartSessionState(cartState);
            TempData["SuccessMessage"] = "آیتم با موفقیت حذف شد.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearCart()
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            cartState.Clear();
            HttpContext.Session.SetCartSessionState(cartState);
            TempData["SuccessMessage"] = "سبد خرید خالی شد.";
            return RedirectToAction("Index");
        }

        [Authorize]
        [HttpGet]
        public IActionResult Checkout()
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            if (cartState.TotalItems == 0)
            {
                TempData["ErrorMessage"] = "سبد خرید شما خالی است.";
                return RedirectToAction("Index");
            }
            return RedirectToAction("Index", "Checkout");
        }

        public IActionResult RedirectToLogin()
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Cart") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscount(string discountCode)
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            if (cartState.TotalItems == 0)
            {
                TempData["DiscountError"] = "سبد خرید خالی است.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(discountCode))
            {
                TempData["DiscountError"] = "لطفاً کد تخفیف را وارد کنید.";
                return RedirectToAction("Index");
            }

            var discount = await _salesDb.Set<DiscountCode>()
                .FirstOrDefaultAsync(d => d.Code == discountCode && d.IsActive
                    && d.StartDate <= DateTime.UtcNow && d.EndDate >= DateTime.UtcNow
                    && (!d.UsageLimit.HasValue || d.UsedCount < d.UsageLimit));

            if (discount == null)
            {
                TempData["DiscountError"] = "کد تخفیف نامعتبر یا منقضی شده است.";
                return RedirectToAction("Index");
            }

            if (discount.MinimumOrderAmount.HasValue && cartState.TotalApproxPrice < discount.MinimumOrderAmount.Value)
            {
                TempData["DiscountError"] = $"حداقل مبلغ سفارش برای این کد {discount.MinimumOrderAmount.Value:N0} تومان است.";
                return RedirectToAction("Index");
            }

            decimal discountAmount = 0;
            if (discount.DiscountType == DiscountType.Percentage)
                discountAmount = cartState.TotalApproxPrice * discount.DiscountValue / 100;
            else
                discountAmount = discount.DiscountValue;

            if (discountAmount > cartState.TotalApproxPrice)
                discountAmount = cartState.TotalApproxPrice;

            cartState.AppliedDiscountCode = discount.Code;
            cartState.AppliedDiscountCodeId = discount.Id;
            cartState.DiscountAmount = discountAmount;

            HttpContext.Session.SetCartSessionState(cartState);
            TempData["DiscountSuccess"] = $"کد تخفیف اعمال شد. مبلغ کاهش یافته: {discountAmount:N0} تومان";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveDiscount()
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            cartState.AppliedDiscountCode = null;
            cartState.AppliedDiscountCodeId = null;
            cartState.DiscountAmount = null;
            HttpContext.Session.SetCartSessionState(cartState);
            TempData["DiscountSuccess"] = "کد تخفیف حذف شد.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult GetCartState()
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            return Ok(new
            {
                totalItems = cartState.TotalItems,
                totalPrice = cartState.TotalApproxPrice,
                discountAmount = cartState.DiscountAmount ?? 0,
                finalPrice = cartState.TotalApproxPrice,
                appliedDiscountCode = cartState.AppliedDiscountCode,
                items = cartState.Items.Select(x => new
                {
                    x.Id,
                    x.BoxTitle,
                    x.TotalRows,
                    x.TotalApproxWeight,
                    totalPrice = x.TotalApproxPrice,
                    itemsCount = x.Items.Count
                })
            });
        }
    }
}
