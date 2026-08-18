using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;
using System;
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

            if (ModelState.IsValid)
            {
                model.UserId = userId;
                model.CreatedAt = DateTime.UtcNow;
                model.Status = CustomCakeOrderStatus.Pending;
                model.IsPaid = false;

                _context.CustomCakeOrders.Add(model);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(userId);
                ViewBag.UserName = user?.FullName ?? user?.UserName ?? "کاربر گرامی";
                return PartialView("_OrderSuccessModal", model.Id);
            }

            return View(model);
        }
    }
}