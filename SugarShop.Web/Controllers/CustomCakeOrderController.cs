using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;
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

        public CustomCakeOrderController(SugarShopSalesDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomCakeOrder model,
            IFormFile? sampleImage, IFormFile? printImage, // ✅ اضافه شدن پارامترهای فایل
            string? desiredDeliveryDatePersian,
            string? desiredDeliveryTime)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            ModelState.Remove("UserId");
            ModelState.Remove("CreatedAt");

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

            if (ModelState.IsValid)
            {
                model.UserId = userId;
                model.CreatedAt = DateTime.UtcNow;
                model.Status = CustomCakeOrderStatus.Pending;
                model.IsPaid = false;

                // ✅ ذخیره تصاویر آپلود شده
                if (sampleImage != null && sampleImage.Length > 0)
                {
                    model.SampleImagePath = await SaveFileAsync(sampleImage, "cake_sample");
                }
                if (printImage != null && printImage.Length > 0)
                {
                    model.PrintImagePath = await SaveFileAsync(printImage, "cake_print");
                }

                _context.CustomCakeOrders.Add(model);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(userId);
                ViewBag.UserName = user?.FullName ?? user?.UserName ?? "کاربر گرامی";
                return PartialView("_OrderSuccessModal", model.Id);
            }

            return View(model);
        }

        // ✅ متد کمکی برای ذخیره فایل
        private async Task<string?> SaveFileAsync(IFormFile file, string prefix)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images/cake_orders");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{prefix}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/cake_orders/{uniqueFileName}";
        }
    }
}