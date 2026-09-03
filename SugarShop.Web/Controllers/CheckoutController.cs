using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.SessionModels;
using SugarShop.Web.ViewModels;
using System.Text.Json;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            SugarShopCatalogDbContext catalogDb,
            SugarShopSalesDbContext salesDb,
            UserManager<ApplicationUser> userManager,
            ILogger<CheckoutController> logger)
        {
            _catalogDb = catalogDb;
            _salesDb = salesDb;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            if (cartState.Items.Count == 0 && cartState.Products.Count == 0)
            {
                TempData["ErrorMessage"] = "سبد خرید شما خالی است. ابتدا محصولی اضافه کنید.";
                return RedirectToAction("Index", "Cart");
            }
            ViewBag.HasBoxes = cartState.Items.Any();
            var userId = _userManager.GetUserId(User);
            var addresses = await _salesDb.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
            if (!addresses.Any())
            {
                TempData["InfoMessage"] = "لطفاً ابتدا یک آدرس برای تحویل سفارش وارد کنید.";
                return RedirectToAction("Create", "Address", new { returnUrl = Url.Action("Index", "Checkout") });
            }

            var model = new CheckoutViewModel
            {
                UserAddresses = addresses,
                SelectedAddressId = addresses.FirstOrDefault(a => a.IsDefault)?.Id ?? addresses.First().Id
            };

            ViewBag.CartItemsCount = cartState.Items.Count + cartState.Products.Count;
            ViewBag.CartTotal = cartState.TotalApproxPrice;
            ViewBag.AppliedDiscountCode = cartState.AppliedDiscountCode;
            ViewBag.DiscountAmount = cartState.DiscountAmount;
            ViewBag.HasBoxes = cartState.Items.Any();
            var wallet = await _salesDb.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            ViewBag.WalletBalance = wallet?.Balance ?? 0;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel vm)
        {
            var cartState = HttpContext.Session.GetCartSessionState();
            if (cartState.Items.Count == 0 && cartState.Products.Count == 0)
                return BadRequest("سبد خرید شما خالی است.");

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            Address address = null;

            if (vm.SelectedAddressId.HasValue && vm.SelectedAddressId.Value > 0)
            {
                address = await _salesDb.Addresses.FindAsync(vm.SelectedAddressId.Value);
                if (address == null || address.UserId != userId)
                    return BadRequest("آدرس انتخابی نامعتبر است.");
                if (string.IsNullOrWhiteSpace(address.ReceiverName) ||
                    string.IsNullOrWhiteSpace(address.ReceiverPhone) ||
                    string.IsNullOrWhiteSpace(address.FullAddress))
                {
                    ModelState.AddModelError("", "آدرس انتخابی ناقص است. لطفاً آن را ویرایش کنید یا آدرس جدید وارد کنید.");
                    return await PrepareFailedCheckoutView(vm);
                }
            }
            else if (vm.UseNewAddress)
            {
                if (string.IsNullOrWhiteSpace(vm.NewFullAddress) ||
                    string.IsNullOrWhiteSpace(vm.NewReceiverName) ||
                    string.IsNullOrWhiteSpace(vm.NewReceiverPhone))
                {
                    ModelState.AddModelError("", "لطفاً تمام فیلدهای آدرس جدید را کامل کنید.");
                    return await PrepareFailedCheckoutView(vm);
                }

                address = new Address
                {
                    UserId = userId,
                    Title = string.IsNullOrWhiteSpace(vm.NewAddressTitle) ? "آدرس جدید" : vm.NewAddressTitle,
                    FullAddress = vm.NewFullAddress,
                    PostalCode = vm.NewPostalCode,
                    ReceiverName = vm.NewReceiverName,
                    ReceiverPhone = vm.NewReceiverPhone,
                    IsDefault = !await _salesDb.Addresses.AnyAsync(a => a.UserId == userId),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _salesDb.Addresses.Add(address);
                await _salesDb.SaveChangesAsync();
            }
            else
            {
                ModelState.AddModelError("", "لطفاً یک آدرس را انتخاب کنید یا آدرس جدید وارد کنید.");
                return await PrepareFailedCheckoutView(vm);
            }

            // Recalculate prices from database, NOT from session
            decimal total = 0;

            // Recalculate box items (sweet items) from DB
            var boxSweetIds = cartState.Items
                .SelectMany(b => b.Items)
                .Select(s => s.SweetItemId)
                .Distinct()
                .ToList();

            var sweetItemsDb = boxSweetIds.Any()
                ? await _catalogDb.SweetItems.Where(s => boxSweetIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id)
                : new Dictionary<int, SweetItem>();

            // Recalculate product items from DB
            var productIds = cartState.Products.Select(p => p.ProductId).Distinct().ToList();
            var productsDb = productIds.Any()
                ? await _catalogDb.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
                : new Dictionary<int, Product>();

            // Verify stock availability for products
            foreach (var cartProduct in cartState.Products)
            {
                if (!productsDb.TryGetValue(cartProduct.ProductId, out var product) || !product.IsActive)
                {
                    ModelState.AddModelError("", $"محصول «{cartProduct.Title}» دیگر موجود نیست.");
                    return await PrepareFailedCheckoutView(vm);
                }
                if (product.Inventory < cartProduct.Quantity)
                {
                    ModelState.AddModelError("", $"موجودی محصول «{product.TitleFa}» کافی نیست. موجودی فعلی: {product.Inventory}");
                    return await PrepareFailedCheckoutView(vm);
                }
            }

            // Calculate total from DB prices
            foreach (var cartItem in cartState.Items)
            {
                foreach (var sweetItem in cartItem.Items)
                {
                    if (sweetItemsDb.TryGetValue(sweetItem.SweetItemId, out var dbSweet))
                    {
                        var rowPrice = (dbSweet.PricePerKg * dbSweet.ApproxWeightGrams) / 1000m;
                        total += rowPrice;
                    }
                }
            }
            foreach (var cartProduct in cartState.Products)
            {
                if (productsDb.TryGetValue(cartProduct.ProductId, out var dbProduct))
                {
                    total += dbProduct.Price * cartProduct.Quantity;
                }
            }

            // Apply discount
            if (cartState.AppliedDiscountCodeId.HasValue)
            {
                var discount = await _salesDb.DiscountCodes.FindAsync(cartState.AppliedDiscountCodeId.Value);
                if (discount != null && discount.IsActive
                    && discount.StartDate <= DateTime.UtcNow && discount.EndDate >= DateTime.UtcNow
                    && (!discount.UsageLimit.HasValue || discount.UsedCount < discount.UsageLimit))
                {
                    decimal discountAmount = discount.DiscountType == DiscountType.Percentage
                        ? total * discount.DiscountValue / 100
                        : discount.DiscountValue;
                    if (discountAmount > total) discountAmount = total;
                    total -= discountAmount;
                }
            }

            // Wallet deduction
            decimal walletUsed = 0;
            if (vm.UseWallet)
            {
                var wallet = await _salesDb.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet != null && wallet.Balance > 0)
                {
                    walletUsed = Math.Min(wallet.Balance, total);
                    wallet.Balance -= walletUsed;
                    wallet.UpdatedAt = DateTime.UtcNow;
                    total -= walletUsed;
                }
            }

            bool hasBoxes = cartState.Items.Any();
            OrderStatus initialStatus = hasBoxes ? OrderStatus.AwaitingReview : OrderStatus.PendingPayment;
            bool paymentEnabled = !hasBoxes;

            using var salesTx = await _salesDb.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    OrderCode = GenerateOrderCode(),
                    UserId = userId,
                    OrderStatus = initialStatus,
                    PaymentStatus = PaymentStatus.Unpaid,
                    TotalAmountSnapshot = total,
                    DiscountAmountSnapshot = cartState.DiscountAmount ?? 0,
                    DiscountCodeId = cartState.AppliedDiscountCodeId,
                    DeliveryFeeSnapshot = 0,
                    TaxAmountSnapshot = 0,
                    CustomerName = address.ReceiverName,
                    CustomerPhone = address.ReceiverPhone,
                    CustomerFullAddress = address.FullAddress,
                    CustomerPostalCode = address.PostalCode ?? "",
                    CustomerAddressId = address.Id,
                    DeliveryDate = vm.DeliveryDate,
                    DeliveryTime = vm.DeliveryTime,
                    Notes = vm.Notes ?? "",
                    IsPaymentEnabled = paymentEnabled,
                    FinalTotalAmount = hasBoxes ? null : total,
                    FinalTotalWeightGrams = hasBoxes ? null : cartState.TotalApproxWeight,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DeliveryMethod = vm.DeliveryMethod
                };
                _salesDb.Orders.Add(order);
                await _salesDb.SaveChangesAsync();

                foreach (var cartItem in cartState.Items)
                {
                    foreach (var sweetItem in cartItem.Items)
                    {
                        if (sweetItemsDb.TryGetValue(sweetItem.SweetItemId, out var dbSweet))
                        {
                            var rowPrice = (dbSweet.PricePerKg * dbSweet.ApproxWeightGrams) / 1000m;
                            _salesDb.OrderItems.Add(new OrderItem
                            {
                                OrderId = order.Id,
                                ItemType = OrderItemType.SweetItem,
                                SweetItemId = sweetItem.SweetItemId,
                                Quantity = 1,
                                UnitPriceSnapshot = rowPrice,
                                WeightSnapshotGrams = dbSweet.ApproxWeightGrams,
                                TotalPriceSnapshot = rowPrice,
                                CreatedAt = DateTime.UtcNow,
                                BoxTypeId = cartItem.BoxTypeId,
                                BoxTitle = cartItem.BoxTitle
                            });
                        }
                    }
                }

                foreach (var productItem in cartState.Products)
                {
                    if (productsDb.TryGetValue(productItem.ProductId, out var dbProduct))
                    {
                        _salesDb.OrderItems.Add(new OrderItem
                        {
                            OrderId = order.Id,
                            ItemType = OrderItemType.Product,
                            ProductId = productItem.ProductId,
                            Quantity = productItem.Quantity,
                            UnitPriceSnapshot = dbProduct.Price,
                            WeightSnapshotGrams = (int?)dbProduct.WeightGrams,
                            TotalPriceSnapshot = dbProduct.Price * productItem.Quantity,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Wallet transaction
                if (vm.UseWallet && walletUsed > 0)
                {
                    _salesDb.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = userId,
                        Amount = -walletUsed,
                        Type = "Purchase",
                        Description = $"استفاده از اعتبار برای سفارش {order.OrderCode}",
                        OrderId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // استفاده از کد تخفیف فقط با UPDATE شرطی؛ اگر هم‌زمان سفارش دیگری
                // کد را به سقف استفاده رسانده باشد، کل ثبت سفارش لغو می‌شود (جلوگیری از استفاده بیش از حد)
                if (cartState.AppliedDiscountCodeId.HasValue)
                {
                    var claimed = await _salesDb.DiscountCodes
                        .Where(c => c.Id == cartState.AppliedDiscountCodeId.Value
                            && c.IsActive
                            && c.StartDate <= DateTime.UtcNow
                            && c.EndDate >= DateTime.UtcNow
                            && (!c.UsageLimit.HasValue || c.UsedCount < c.UsageLimit))
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1));

                    if (claimed == 0)
                        throw new InvalidOperationException("DiscountNoLongerAvailable");
                }

                await _salesDb.SaveChangesAsync();
                await salesTx.CommitAsync();

                // Clear cart
                HttpContext.Session.SetCartSessionState(new CartSessionState());
                HttpContext.Session.SetBoxSessionState(new BoxSessionState());

                if (!hasBoxes)
                {
                    return RedirectToAction("RequestPayment", "Payment", new { orderId = order.Id });
                }
                else
                {
                    return RedirectToAction("Success", new { orderId = order.Id, orderCode = order.OrderCode });
                }
            }
            catch (InvalidOperationException ex) when (ex.Message == "DiscountNoLongerAvailable")
            {
                await salesTx.RollbackAsync();
                _logger.LogWarning("Discount code no longer available during order placement (concurrent usage). Order for user {UserId} rolled back.", userId);
                ModelState.AddModelError("", "کد تخفیف دیگر معتبر نیست یا به سقف استفاده رسیده است.");
                return await PrepareFailedCheckoutView(vm);
            }
            catch (Exception ex)
            {
                await salesTx.RollbackAsync();
                _logger.LogError(ex, "Error placing order for user {UserId}", userId);
                ModelState.AddModelError("", "خطا در ثبت سفارش. لطفاً دوباره تلاش کنید.");
                return await PrepareFailedCheckoutView(vm);
            }
        }

        [HttpGet]
        public IActionResult Success(int orderId, string orderCode)
        {
            ViewBag.OrderId = orderId;
            ViewBag.OrderCode = orderCode;
            return View();
        }

        private async Task<IActionResult> PrepareFailedCheckoutView(CheckoutViewModel vm)
        {
            var userId = _userManager.GetUserId(User);
            var addresses = await _salesDb.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
            vm.UserAddresses = addresses;

            var wallet = await _salesDb.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            ViewBag.WalletBalance = wallet?.Balance ?? 0;

            var cartState = HttpContext.Session.GetCartSessionState();
            ViewBag.CartItemsCount = cartState.Items.Count + cartState.Products.Count;
            ViewBag.CartTotal = cartState.TotalApproxPrice;
            ViewBag.AppliedDiscountCode = cartState.AppliedDiscountCode;
            ViewBag.DiscountAmount = cartState.DiscountAmount;

            return View("Index", vm);
        }

        private string GenerateOrderCode()
        {
            var d = DateTime.UtcNow;
            return $"ORD-{d:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        }
    }
}
