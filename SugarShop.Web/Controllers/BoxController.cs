using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.SessionModels;

namespace SugarShop.Web.Controllers
{
        public class BoxController : Controller
    {
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly UserManager<ApplicationUser> _userManager;

        public BoxController(SugarShopCatalogDbContext catalogDb, SugarShopSalesDbContext salesDb, UserManager<ApplicationUser> userManager)
        {
            _catalogDb = catalogDb;
            _salesDb = salesDb;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Status(int? returnToOrder = null, int? sweetId = null)
        {
            var state = HttpContext.Session.GetBoxSessionState();
            if (sweetId.HasValue && state.SelectedBoxTypeId.HasValue)
            {
                var sweetItem = await _catalogDb.SweetItems.FindAsync(sweetId.Value);
                if (sweetItem != null && sweetItem.IsActive && sweetItem.InventoryCount > 0)
                {
                    var boxType = await _catalogDb.BoxTypes.FindAsync(state.SelectedBoxTypeId.Value);
                    if (boxType != null)
                    {
                        if (state.TotalRowsUsed < boxType.MaxRows &&
                            state.TotalApproxWeightGrams + sweetItem.ApproxWeightGrams <= boxType.CapacityGrams)
                        {
                            state.Items.Add(new BoxSelectionSessionItem
                            {
                                SweetItemId = sweetItem.Id,
                                TitleFaSnapshot = sweetItem.TitleFa,
                                ApproxWeightGrams = sweetItem.ApproxWeightGrams,
                                PricePerKg = sweetItem.PricePerKg
                            });
                            HttpContext.Session.SetBoxSessionState(state);
                            TempData["BoxFullMessage"] = "✅ شیرینی با موفقیت به جعبه اضافه شد.";
                        }
                        else
                        {
                            TempData["BoxFullMessage"] = "❌ ظرفیت جعبه کامل است یا خطایی رخ داده است.";
                        }
                    }
                }
            }
            BoxType? selectedBoxType = null;
            if (state.SelectedBoxTypeId.HasValue)
            {
                selectedBoxType = await _catalogDb.BoxTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == state.SelectedBoxTypeId.Value && b.IsActive);
            }

            var boxTypes = await _catalogDb.BoxTypes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var sweetItems = await _catalogDb.SweetItems
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Include(x => x.Category)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            ViewBag.BoxTypes = boxTypes;
            ViewBag.SelectedBoxType = selectedBoxType;
            ViewBag.SweetItems = sweetItems;
            ViewBag.ReturnToOrder = returnToOrder;

            return View(state);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectBoxType(int boxTypeId)
        {
            var boxType = await _catalogDb.BoxTypes
                .FirstOrDefaultAsync(b => b.Id == boxTypeId && b.IsActive);
            if (boxType == null) return BadRequest("نوع جعبه نامعتبر است.");

            var state = HttpContext.Session.GetBoxSessionState();
            if (state.Items.Count > 0) return BadRequest("برای تغییر نوع جعبه ابتدا جعبه را خالی کنید.");

            state.SelectedBoxTypeId = boxType.Id;
            HttpContext.Session.SetBoxSessionState(state);
            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRow(int sweetItemId)
        {
            var state = HttpContext.Session.GetBoxSessionState();
            if (state.SelectedBoxTypeId == null)
                return Json(new { success = false, title = "⚠️ خطا", message = "ابتدا نوع جعبه را انتخاب کنید.", type = "error" });

            var boxType = await _catalogDb.BoxTypes.FirstOrDefaultAsync(b => b.Id == state.SelectedBoxTypeId.Value && b.IsActive);
            if (boxType == null)
                return Json(new { success = false, title = "⚠️ خطا", message = "نوع جعبه نامعتبر است.", type = "error" });

            var sweetItem = await _catalogDb.SweetItems.FirstOrDefaultAsync(s => s.Id == sweetItemId && s.IsActive);
            if (sweetItem == null)
                return Json(new { success = false, title = "⚠️ خطا", message = "شیرینی نامعتبر است.", type = "error" });

            bool isOutOfStock = sweetItem.InventoryCount <= 0;

            if (state.TotalRowsUsed + 1 > boxType.MaxRows)
                return Json(new { success = false, title = "⚠️ خطا", message = $"حداکثر تعداد ردیف مجاز {boxType.MaxRows} است.", type = "error" });

            if (state.TotalApproxWeightGrams + sweetItem.ApproxWeightGrams > boxType.CapacityGrams)
                return Json(new { success = false, title = "⚠️ خطا", message = $"ظرفیت وزنی جعبه ({boxType.CapacityGrams} گرم) کامل شده است.", type = "error" });

            state.Items.Add(new BoxSelectionSessionItem
            {
                SweetItemId = sweetItem.Id,
                TitleFaSnapshot = sweetItem.TitleFa,
                ApproxWeightGrams = sweetItem.ApproxWeightGrams,
                PricePerKg = sweetItem.PricePerKg
            });
            HttpContext.Session.SetBoxSessionState(state);

            bool isFull = (state.TotalRowsUsed >= boxType.MaxRows) || (state.TotalApproxWeightGrams >= boxType.CapacityGrams);

            if (isOutOfStock)
            {
                return Json(new
                {
                    success = true,
                    title = "⚠️ ناموجود",
                    message = $"موجودی {sweetItem.TitleFa} در حال حاضر به اتمام رسیده است. ولی نگران نباشید! میتوانید به جعبه خود اضافه کنید. ما بلافاصله برایتان موجود میکنیم.",
                    type = "warning",
                    isFull = isFull
                });
            }

            if (isFull)
                return Json(new { success = true, title = "🎉 جعبه کامل شد!", message = "جعبه شما با موفقیت پر شد. برای ادامه به سبد خرید بروید.", type = "success", isFull = true });
            else
                return Json(new { success = true, title = "✅ افزوده شد", message = $"شیرینی {sweetItem.TitleFa} با موفقیت به جعبه اضافه شد.", type = "success" });
        }
        [HttpGet]
        public async Task<IActionResult> GetBoxModalContent()
        {
            var state = HttpContext.Session.GetBoxSessionState();

            BoxType? selectedBoxType = null;
            if (state.SelectedBoxTypeId.HasValue)
            {
                selectedBoxType = await _catalogDb.BoxTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == state.SelectedBoxTypeId.Value && b.IsActive);
            }

            var boxTypes = await _catalogDb.BoxTypes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var sweetItems = await _catalogDb.SweetItems
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            ViewBag.SelectedBoxType = selectedBoxType;
            ViewBag.BoxTypes = boxTypes;
            ViewBag.SweetItems = sweetItems;

            return PartialView("_BoxContent", state);
        }
        [HttpGet]
        public IActionResult GetCurrentBoxType()
        {
            var state = HttpContext.Session.GetBoxSessionState();
            return Ok(new { selectedBoxId = state.SelectedBoxTypeId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveRow(int rowIndex)
        {
            var state = HttpContext.Session.GetBoxSessionState();
            if (rowIndex >= 0 && rowIndex < state.Items.Count)
            {
                state.Items.RemoveAt(rowIndex);
                HttpContext.Session.SetBoxSessionState(state);
            }
            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplaceRow(int rowIndex, int sweetItemId)
        {
            var state = HttpContext.Session.GetBoxSessionState();
            if (rowIndex < 0 || rowIndex >= state.Items.Count) return BadRequest("ردیف نامعتبر است.");
            if (state.SelectedBoxTypeId == null) return BadRequest("نوع جعبه انتخاب نشده است.");

            var boxType = await _catalogDb.BoxTypes
                .FirstOrDefaultAsync(b => b.Id == state.SelectedBoxTypeId.Value && b.IsActive);
            if (boxType == null) return BadRequest("نوع جعبه نامعتبر است.");

            var sweetItem = await _catalogDb.SweetItems
                .FirstOrDefaultAsync(s => s.Id == sweetItemId && s.IsActive);
            if (sweetItem == null) return BadRequest("شیرینی نامعتبر است.");

            var oldItem = state.Items[rowIndex];
            int newTotalWeight = state.TotalApproxWeightGrams - oldItem.ApproxWeightGrams + sweetItem.ApproxWeightGrams;
            if (newTotalWeight > boxType.CapacityGrams)
                return BadRequest("وزن این شیرینی بیشتر از ظرفیت خالی جعبه است.");

            state.Items[rowIndex] = new BoxSelectionSessionItem
            {
                SweetItemId = sweetItem.Id,
                TitleFaSnapshot = sweetItem.TitleFa,
                ApproxWeightGrams = sweetItem.ApproxWeightGrams,
                PricePerKg = sweetItem.PricePerKg
            };

            HttpContext.Session.SetBoxSessionState(state);
            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeBoxType(int newBoxTypeId)
        {
            var state = HttpContext.Session.GetBoxSessionState();
            var newBoxType = await _catalogDb.BoxTypes
                .FirstOrDefaultAsync(b => b.Id == newBoxTypeId && b.IsActive);
            if (newBoxType == null) return BadRequest("نوع جعبه نامعتبر است.");

            if (state.TotalRowsUsed > newBoxType.MaxRows)
                return BadRequest($"جعبه جدید فقط {newBoxType.MaxRows} ردیف ظرفیت دارد.");
            if (state.TotalApproxWeightGrams > newBoxType.CapacityGrams)
                return BadRequest($"جعبه جدید فقط {newBoxType.CapacityGrams} گرم ظرفیت دارد.");

            state.SelectedBoxTypeId = newBoxType.Id;
            HttpContext.Session.SetBoxSessionState(state);
            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            var state = HttpContext.Session.GetBoxSessionState();
            state.Items.Clear();
            HttpContext.Session.SetBoxSessionState(state);
            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int? orderId = null)
        {
            var boxState = HttpContext.Session.GetBoxSessionState();
            if (boxState.Items.Count == 0) return BadRequest("جعبه خالی است.");
            if (boxState.SelectedBoxTypeId == null) return BadRequest("نوع جعبه انتخاب نشده است.");
            var boxType = await _catalogDb.BoxTypes.FindAsync(boxState.SelectedBoxTypeId.Value);
            if (boxType == null) return BadRequest("نوع جعبه نامعتبر است.");
            if (orderId.HasValue)
            {
                var order = await _salesDb.Orders.FindAsync(orderId.Value);
                if (order == null) return NotFound("سفارش یافت نشد.");

                // 🔒 امنیت: فقط کارکنان (Admin/Owner/OrderManager) یا صاحب سفارشِ هنوز بررسی‌نشده/پرداخت‌نشده
                // می‌توانند ردیف شیرینی به سفارش اضافه کنند؛ سفارش‌های نهایی‌شده/پرداخت‌شده قابل تغییر نیستند.
                bool isStaff = User.IsInRole("Admin") || User.IsInRole("Owner") || User.IsInRole("OrderManager");
                bool isOwnerOfEditableOrder = order.UserId != null
                    && order.UserId == _userManager.GetUserId(User)
                    && order.OrderStatus == OrderStatus.AwaitingReview
                    && order.PaymentStatus == PaymentStatus.Unpaid;
                if (!isStaff && !isOwnerOfEditableOrder)
                    return Unauthorized();

                var uniqueBoxTitle = $"{boxType.TitleFa} ({Guid.NewGuid().ToString("N").Substring(0, 4)})";
                foreach (var item in boxState.Items)
                {
                    var rowPrice = (item.PricePerKg * item.ApproxWeightGrams) / 1000m;
                    _salesDb.OrderItems.Add(new OrderItem
                    {
                        OrderId = orderId.Value,
                        ItemType = OrderItemType.SweetItem,
                        SweetItemId = item.SweetItemId,
                        Quantity = 1,
                        UnitPriceSnapshot = rowPrice,
                        WeightSnapshotGrams = item.ApproxWeightGrams,
                        TotalPriceSnapshot = rowPrice,
                        CreatedAt = DateTime.UtcNow,
                        BoxTypeId = boxType.Id,
                        BoxTitle = uniqueBoxTitle
                    });
                }
                await _salesDb.SaveChangesAsync();

                boxState.Items.Clear();
                HttpContext.Session.SetBoxSessionState(boxState);

                return RedirectToAction("OrderDetails", "Admin", new { id = orderId.Value });
            }
            else
            {
                var cartState = HttpContext.Session.GetCartSessionState();
                var cartItem = new CartItem
                {
                    BoxTypeId = boxType.Id,
                    BoxTitle = boxType.TitleFa,
                    BoxCapacityGrams = boxType.CapacityGrams,
                    BoxMaxRows = boxType.MaxRows,
                    Items = new List<BoxSelectionSessionItem>(boxState.Items)
                };
                cartState.Items.Add(cartItem);
                HttpContext.Session.SetCartSessionState(cartState);

                boxState.Items.Clear();
                HttpContext.Session.SetBoxSessionState(boxState);

                return Ok(new { cartItemCount = cartState.TotalItems });
            }
        }
    }
}