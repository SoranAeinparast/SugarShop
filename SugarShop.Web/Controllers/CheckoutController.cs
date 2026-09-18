using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Infrastructure.Services;
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

        private readonly InventoryService _inventoryService;

        public CheckoutController(
            SugarShopCatalogDbContext catalogDb,
            SugarShopSalesDbContext salesDb,
            UserManager<ApplicationUser> userManager,
            InventoryService inventoryService,
            ILogger<CheckoutController> logger,
            SugarShop.Web.Services.Sms.SmsService smsService)
        {
            _catalogDb = catalogDb;
            _salesDb = salesDb;
            _userManager = userManager;
            _inventoryService = inventoryService;
            _logger = logger;
            _sms = smsService;
        }

        private readonly SugarShop.Web.Services.Sms.SmsService _sms;

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

            // 🔒 امنیت مالی: ردیف‌های با تعداد نامعتبر (صفر یا منفی) باعث معکوس شدن محاسبه جمع
            // و در نتیجه بستانکار شدن اشتباه کیف پول می‌شدند؛ چنین سبدی ثبت نمی‌شود.
            var invalidProductLines = cartState.Products.Where(p => p.Quantity < 1).ToList();
            if (invalidProductLines.Any())
            {
                foreach (var invalid in invalidProductLines)
                    cartState.RemoveProduct(invalid.ProductId);
                HttpContext.Session.SetCartSessionState(cartState);
                _logger.LogWarning("Cart of user {UserId} contained {Count} invalid product line(s); removed and order rejected.",
                    userId, invalidProductLines.Count);
                TempData["ErrorMessage"] = "سبد خرید شما تعداد نامعتبر داشت و اصلاح شد. لطفاً سبد را بررسی و دوباره ثبت کنید.";
                return RedirectToAction("Index", "Cart");
            }

            Address? address = null;

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

            // ═══ محاسبه جداگانه بخش «قیمت‌ثابت» (محصولات) و بخش «وزن‌کشی» (جعبه‌ها) ═══
            // مدل استاندارد فروش شیرینی/کیک: محصولات قیمت‌ثابت بلافاصله قابل پرداخت‌اند؛
            // جعبه‌های نیازمند وزن‌کشی پس از اعلام قیمت فروشگاه قابل پرداخت می‌شوند (بیعانه/پرداخت مرحله‌ای).
            decimal productTotal = 0;   // جمع قیمت‌ثابت — قابل پرداخت همان لحظه
            decimal boxApproxTotal = 0; // جمع تقریبی جعبه‌ها — پس از وزن‌کشی نهایی می‌شود

            foreach (var cartItem in cartState.Items)
            {
                foreach (var sweetItem in cartItem.Items)
                {
                    if (sweetItemsDb.TryGetValue(sweetItem.SweetItemId, out var dbSweet))
                    {
                        var rowPrice = (dbSweet.PricePerKg * dbSweet.ApproxWeightGrams) / 1000m;
                        boxApproxTotal += rowPrice;
                    }
                }
            }
            foreach (var cartProduct in cartState.Products)
            {
                if (productsDb.TryGetValue(cartProduct.ProductId, out var dbProduct))
                {
                    productTotal += dbProduct.Price * cartProduct.Quantity;
                }
            }
            total = productTotal + boxApproxTotal;
            // جمع «قیمت‌ثابت» پیش از تخفیف/کیف پول — مبنای تشخیص تسویه کامل در همین لحظه
            decimal rawProductTotal = productTotal;

            // کد تخفیف روی بخش «قیمت‌ثابت» (قابل پرداخت فوری) اعمال می‌شود؛
            // بخش وزن‌کشی هنوز قیمت واقعی ندارد و اعمال تخفیف روی آن نادرست است.
            decimal discountApplied = 0;
            if (cartState.AppliedDiscountCodeId.HasValue)
            {
                var discount = await _salesDb.DiscountCodes.FindAsync(cartState.AppliedDiscountCodeId.Value);
                if (discount != null && discount.IsActive
                    && discount.StartDate <= DateTime.UtcNow && discount.EndDate >= DateTime.UtcNow
                    && (!discount.UsageLimit.HasValue || discount.UsedCount < discount.UsageLimit))
                {
                    decimal discountAmount = discount.DiscountType == DiscountType.Percentage
                        ? productTotal * discount.DiscountValue / 100
                        : discount.DiscountValue;
                    if (discountAmount > productTotal) discountAmount = productTotal;
                    discountApplied = discountAmount;
                    productTotal -= discountAmount;
                }
            }

            // ── مصرف اعتبار کیف پول (روی بخش قابل پرداخت) ──
            // مبلغ درخواستی از روی موجودی همین لحظه محاسبه می‌شود، اما کسر واقعی به‌صورت اتمیک
            // و داخل تراکنش با یک UPDATE شرطی انجام می‌شود تا دو سفارش هم‌زمان نتوانند
            // یک موجودی را دو بار خرج کنند (موجودی منفی / پرداخت مضاعف).
            decimal requestedWalletUse = 0;
            if (vm.UseWallet && productTotal > 0)
            {
                var wallet = await _salesDb.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet != null && wallet.Balance > 0)
                {
                    requestedWalletUse = Math.Min(wallet.Balance, productTotal);
                    if (requestedWalletUse < 0) requestedWalletUse = 0;
                }
            }
            productTotal -= requestedWalletUse;

            bool hasBoxes = cartState.Items.Any();
            // سفارش ترکیبی/جعبه‌ای: پرداختِ بخش قیمت‌ثابت همان لحظه فعال است؛
            // اگر هیچ مبلغ قابل پرداختی نباشد (مثلاً فقط جعبه یا پوشش کامل با کیف پول)، پرداخت غیرفعال می‌ماند
            // تا پس از وزن‌کشی توسط فروشگاه فعال شود.
            decimal payableNow = productTotal; // مبلغ قابل پرداخت همین حالا

            // بخش «قیمت‌ثابت» همین حالا کامل تسویه شده است (با کیف پول یا تخفیف) و مبلغی برای درگاه نمی‌ماند
            bool fixedPortionFullySettled = rawProductTotal > 0 && payableNow <= 0;

            OrderStatus initialStatus = hasBoxes ? OrderStatus.AwaitingReview : OrderStatus.PendingPayment;
            PaymentStatus initialPaymentStatus = PaymentStatus.Unpaid;
            if (fixedPortionFullySettled && !hasBoxes)
            {
                // سفارش بدون جعبه که کاملاً با کیف پول/تخفیف تسویه شد → همان لحظه پرداخت‌شده است
                initialStatus = OrderStatus.Paid;
                initialPaymentStatus = PaymentStatus.Succeeded;
            }

            bool paymentEnabled = payableNow > 0;

            using var salesTx = await _salesDb.Database.BeginTransactionAsync();
            try
            {
                // کسر اتمیک از کیف پول: فقط اگر موجودی همین لحظه کافی باشد؛ در غیر این صورت کل
                // ثبت سفارش لغو می‌شود تا اعتبار بیش از موجودی خرج نشود.
                decimal walletUsed = 0;
                if (requestedWalletUse > 0)
                {
                    var walletDebited = await _salesDb.Wallets
                        .Where(w => w.UserId == userId && w.Balance >= requestedWalletUse)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(w => w.Balance, w => w.Balance - requestedWalletUse)
                            .SetProperty(w => w.UpdatedAt, DateTime.UtcNow));

                    if (walletDebited == 0)
                        throw new InvalidOperationException("WalletBalanceChanged");

                    walletUsed = requestedWalletUse;
                }

                var order = new Order
                {
                    OrderCode = GenerateOrderCode(),
                    UserId = userId,
                    OrderStatus = initialStatus,
                    PaymentStatus = initialPaymentStatus,
                    // برآورد کل سفارش برای گزارش‌ها (بخش جعبه تقریبی است)
                    TotalAmountSnapshot = total,
                    DiscountAmountSnapshot = discountApplied,
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
                    Notes = SanitizeCustomerNotes(vm.Notes),
                    IsPaymentEnabled = paymentEnabled,
                    // مبلغ قابل پرداختِ فعلی: بخش قیمت‌ثابت (پس از وزن‌کشی به‌روز می‌شود)
                    FinalTotalAmount = payableNow,
                    FinalTotalWeightGrams = hasBoxes ? null : cartState.TotalApproxWeight,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DeliveryMethod = vm.DeliveryMethod
                };
                _salesDb.Orders.Add(order);
                await _salesDb.SaveChangesAsync();

                var orderItems = new List<OrderItem>();

                foreach (var cartItem in cartState.Items)
                {
                    foreach (var sweetItem in cartItem.Items)
                    {
                        if (sweetItemsDb.TryGetValue(sweetItem.SweetItemId, out var dbSweet))
                        {
                            var rowPrice = (dbSweet.PricePerKg * dbSweet.ApproxWeightGrams) / 1000m;
                            var sweetOrderItem = new OrderItem
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
                            };
                            _salesDb.OrderItems.Add(sweetOrderItem);
                            orderItems.Add(sweetOrderItem);
                        }
                    }
                }

                foreach (var productItem in cartState.Products)
                {
                    if (productsDb.TryGetValue(productItem.ProductId, out var dbProduct))
                    {
                        var productOrderItem = new OrderItem
                        {
                            OrderId = order.Id,
                            ItemType = OrderItemType.Product,
                            ProductId = productItem.ProductId,
                            Quantity = productItem.Quantity,
                            UnitPriceSnapshot = dbProduct.Price,
                            WeightSnapshotGrams = (int?)dbProduct.WeightGrams,
                            TotalPriceSnapshot = dbProduct.Price * productItem.Quantity,
                            CreatedAt = DateTime.UtcNow
                        };
                        _salesDb.OrderItems.Add(productOrderItem);
                        orderItems.Add(productOrderItem);
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

                // ── کسر موجودی انبار (فقط یک‌بار برای هر سفارش) ──
                // بخش قیمت‌ثابت که همین حالا کامل تسویه شده (کیف پول/تخفیف) باید موجودی‌اش همین لحظه کم شود؛
                // در غیر این صورت کسر هنگام پرداخت موفق در PaymentController.Callback انجام می‌شود.
                if (fixedPortionFullySettled)
                    await DeductInventoryOnceAsync(order, orderItems);

                await _salesDb.SaveChangesAsync();
                await salesTx.CommitAsync();

                // Clear cart
                HttpContext.Session.SetCartSessionState(new CartSessionState());
                HttpContext.Session.SetBoxSessionState(new BoxSessionState());

                // ── اطلاع‌رسانی پیامکی ثبت سفارش (مشتری + کارکنان) — از طریق صف، بدون کندی سایت ──
                try
                {
                    await _sms.NotifyNewOrderAsync(order, needsWeighing: hasBoxes);
                }
                catch (Exception smsEx)
                {
                    _logger.LogWarning(smsEx, "SMS notify for new order {OrderId} failed; order continues.", order.Id);
                }

                if (payableNow > 0)
                {
                    // پرداخت بخش قیمت‌ثابت همان لحظه شروع می‌شود (حتی برای سفارش ترکیبی).
                    // در سفارش ترکیبی (محصولات + جعبه) پیش از درگاه، صفحه «تأیید و پرداخت»
                    // با ریز مبلغ و وضعیت جعبه‌های در انتظار وزن‌کشی به مشتری نشان داده می‌شود
                    // تا بداند همین حالا چه چیزی را می‌پردازد و چه مبلغی به مرحله دوم مانده است.
                    if (hasBoxes)
                        return RedirectToAction("Confirm", "Payment", new { orderId = order.Id });

                    return RedirectToAction("RequestPayment", "Payment", new { orderId = order.Id });
                }
                else
                {
                    // کل مبلغ ثابت با کیف پول پوشش داده شده — نیازی به درگاه نیست
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
            catch (InvalidOperationException ex) when (ex.Message == "WalletBalanceChanged")
            {
                await salesTx.RollbackAsync();
                _logger.LogWarning("Wallet balance of user {UserId} changed concurrently; order rolled back.", userId);
                ModelState.AddModelError("", "موجودی کیف پول شما تغییر کرده است. لطفاً صفحه را بازخوانی کنید و دوباره ثبت کنید.");
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

        /// <summary>
        /// پاک‌سازی متن آزاد «توضیحات» مشتری. ستون Notes هم‌زمان حامل نشانه‌های داخلی سیستم
        /// (WalletRecharge / CustomCakeOrder_) است که مسیر پردازش پرداخت را تغییر می‌دهند؛
        /// اگر مشتری بتواند آن‌ها را جعل کند، مثلاً می‌تواند سفارش کیک سفارشی دیگری را
        /// «پرداخت‌شده» کند. بنابراین هر ورودی کاربر که با این نشانه‌ها شروع شود دور ریخته می‌شود.
        /// </summary>
        private static string SanitizeCustomerNotes(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return string.Empty;

            var trimmed = notes.Trim();
            if (trimmed.StartsWith(OrderNotes.WalletRecharge, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(OrderNotes.CustomCakeOrderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return trimmed;
        }

        /// <summary>
        /// کسر موجودی انبار به‌صورت «فقط یک‌بار برای هر سفارش».
        /// نشانه InventoryDeductedAt در همان تراکنش ذخیره می‌شود، پس در پرداخت مرحله‌ای
        /// (پیش‌پرداخت + تسویه پس از وزن‌کشی) موجودی دو بار کم نمی‌شود.
        /// </summary>
        private async Task DeductInventoryOnceAsync(Order order, IEnumerable<OrderItem> items)
        {
            if (order.InventoryDeductedAt != null) return;

            if (!ReferenceEquals(_catalogDb.Database.GetDbConnection(), _salesDb.Database.GetDbConnection()))
                throw new InvalidOperationException("Sales and Catalog DbContexts are not sharing a connection; inventory deduction aborted.");

            var tx = _salesDb.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Inventory deduction requires an active transaction.");
            _catalogDb.Database.UseTransaction(tx.GetDbTransaction());

            // ابتدا نشانه‌گذاری و سپس کسر؛ اگر کسر خطا بدهد، تراکنش جاری کل عملیات را برمی‌گرداند
            order.InventoryDeductedAt = DateTime.UtcNow;
            await _inventoryService.DecreaseInventoryAsync(items);
        }
    }
}
