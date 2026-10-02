using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;
using SugarShop.Web.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class CustomCakeOrderController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ImageStorageService _images;

        public CustomCakeOrderController(SugarShopSalesDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            SugarShop.Web.Services.Sms.SmsService sms,
            ImageStorageService images)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _sms = sms;
            _images = images;
        }

        private readonly SugarShop.Web.Services.Sms.SmsService _sms;

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            ViewBag.CakeDeliveryAddresses = await _context.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomCakeOrder model,
            IFormFile? sampleImage, IFormFile? printImage, // ✅ اضافه شدن پارامترهای فایل
            string? desiredDeliveryDatePersian,
            string? desiredDeliveryTime,
            DeliveryMethod deliveryMethod,
            int? addressId,
            bool useNewAddress,
            string? newAddressTitle,
            string? newFullAddress,
            string? newPostalCode,
            string? newReceiverName,
            string? newReceiverPhone)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            ModelState.Remove("UserId");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("AddressId");
            ModelState.Remove("ReceiverName");
            ModelState.Remove("ReceiverPhone");
            ModelState.Remove("CustomerFullAddress");
            ModelState.Remove("CustomerPostalCode");
            ModelState.Remove("DeliveryFee");
            ModelState.Remove("DeliveryMethod");

            if (string.IsNullOrWhiteSpace(model.Flavor))
                ModelState.AddModelError("Flavor", "لطفاً طعم کیک را وارد کنید.");
            if (string.IsNullOrWhiteSpace(model.Shape))
                ModelState.AddModelError("Shape", "لطفاً شکل کیک را وارد کنید.");
            if (!model.WeightGrams.HasValue || model.WeightGrams.Value < 500)
                ModelState.AddModelError("WeightGrams", "وزن تقریبی باید حداقل ۵۰۰ گرم باشد.");
            if (!model.Servings.HasValue || model.Servings.Value < 1)
                ModelState.AddModelError("Servings", "تعداد نفرات را وارد کنید.");

            if (!string.IsNullOrEmpty(desiredDeliveryDatePersian) && !string.IsNullOrEmpty(desiredDeliveryTime))
            {
                var deliveryDateTime = PersianDateHelper.ConvertPersianToDateTime(desiredDeliveryDatePersian, desiredDeliveryTime);
                if (deliveryDateTime.HasValue)
                    model.DesiredDeliveryDateTime = deliveryDateTime.Value;
                else
                    ModelState.AddModelError("DesiredDeliveryDateTime", "تاریخ یا ساعت تحویل نامعتبر است.");
            }

            // اعتبارسنجی تصاویر (فرمت و حجم) قبل از ذخیره
            if (sampleImage != null && sampleImage.Length > 0 &&
                !ImageUploadValidator.IsValidImage(sampleImage, ImageUploadValidator.MaxCakeImageBytes, out var sampleImageError))
                ModelState.AddModelError("sampleImage", sampleImageError!);
            if (printImage != null && printImage.Length > 0 &&
                !ImageUploadValidator.IsValidImage(printImage, ImageUploadValidator.MaxCakeImageBytes, out var printImageError))
                ModelState.AddModelError("printImage", printImageError!);

            // ✅ روش تحویل: در صورت ارسال با پیک، آدرس تحویل الزامی است
            model.DeliveryMethod = deliveryMethod;
            if (deliveryMethod == DeliveryMethod.Delivery)
            {
                Address? deliveryAddress = null;
                if (addressId.HasValue)
                {
                    deliveryAddress = await _context.Addresses
                        .FirstOrDefaultAsync(a => a.Id == addressId.Value && a.UserId == userId);
                    if (deliveryAddress == null)
                        ModelState.AddModelError("AddressId", "آدرس انتخابی نامعتبر است.");
                }
                else if (useNewAddress &&
                         !string.IsNullOrWhiteSpace(newFullAddress) &&
                         !string.IsNullOrWhiteSpace(newReceiverName) &&
                         !string.IsNullOrWhiteSpace(newReceiverPhone))
                {
                    deliveryAddress = new Address
                    {
                        UserId = userId,
                        Title = string.IsNullOrWhiteSpace(newAddressTitle) ? "آدرس جدید" : newAddressTitle!.Trim(),
                        FullAddress = newFullAddress!.Trim(),
                        PostalCode = string.IsNullOrWhiteSpace(newPostalCode) ? null : newPostalCode.Trim(),
                        ReceiverName = newReceiverName!.Trim(),
                        ReceiverPhone = newReceiverPhone!.Trim(),
                        IsDefault = !await _context.Addresses.AnyAsync(a => a.UserId == userId),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Addresses.Add(deliveryAddress);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    ModelState.AddModelError("AddressId", "برای ارسال با پیک، لطفاً آدرس تحویل را انتخاب کنید یا آدرس جدید وارد کنید.");
                }

                if (deliveryAddress != null)
                {
                    model.AddressId = deliveryAddress.Id;
                    model.ReceiverName = deliveryAddress.ReceiverName;
                    model.ReceiverPhone = deliveryAddress.ReceiverPhone;
                    model.CustomerFullAddress = deliveryAddress.FullAddress;
                    model.CustomerPostalCode = deliveryAddress.PostalCode;
                }
            }
            else
            {
                model.AddressId = null;
                model.ReceiverName = null;
                model.ReceiverPhone = null;
                model.CustomerFullAddress = null;
                model.CustomerPostalCode = null;
            }

            // ✅ ذخیره تصاویر آپلودشده فقط وقتی سایر اعتبارسنجی‌ها پاس شده‌اند
            // (اعتبارسنجی نوع/حجم داخل ImageStorageService هم دوباره انجام می‌شود)
            if (ModelState.IsValid)
            {
                var (savedSamplePath, sampleSaveError) = await _images.SaveImageAsync(sampleImage, "cake_sample", ImageUploadValidator.MaxCakeImageBytes);
                if (sampleSaveError != null)
                    ModelState.AddModelError("sampleImage", sampleSaveError);
                else
                    model.SampleImagePath = savedSamplePath;

                var (savedPrintPath, printSaveError) = await _images.SaveImageAsync(printImage, "cake_print", ImageUploadValidator.MaxCakeImageBytes);
                if (printSaveError != null)
                    ModelState.AddModelError("printImage", printSaveError);
                else
                    model.PrintImagePath = savedPrintPath;
            }

            if (ModelState.IsValid)
            {
                model.UserId = userId;
                model.CreatedAt = DateTime.UtcNow;
                model.Status = CustomCakeOrderStatus.Pending;
                model.IsPaid = false;

                _context.CustomCakeOrders.Add(model);
                await _context.SaveChangesAsync();

                // ── اعلام سفارش کیک جدید به مدیر/سرآشپز + تأیید به مشتری ──
                try
                {
                    await _sms.NotifyNewCustomCakeAsync(model);
                    await _sms.SendScenarioAsync(model.ReceiverPhone, SugarShop.Domain.Entities.Sms.SmsScenario.OrderPlacedCustomer,
                        new Dictionary<string, string>
                        {
                            { "CustomerName", (model.ReceiverName ?? "مشتری").Split(' ')[0] },
                            { "OrderCode", model.Id.ToString() }
                        });
                }
                catch { /* پیامک نباید ثبت سفارش را متوقف کند */ }

                var user = await _userManager.FindByIdAsync(userId);
                ViewBag.UserName = user?.FullName ?? user?.UserName ?? "کاربر گرامی";
                return PartialView("_OrderSuccessModal", model.Id);
            }

            // درخواست AJAX: فقط فرم (بدون layout) برگردانده می‌شود تا صفحه خراب نشود
            ViewBag.CakeDeliveryAddresses = await _context.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
            ViewBag.CakeDeliveryMethod = (int)model.DeliveryMethod;
            ViewBag.CakeDeliveryAddressId = model.AddressId;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateForm", model);

            return View(model);
        }
    }
}