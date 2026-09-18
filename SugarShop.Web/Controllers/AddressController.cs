using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.SessionModels;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class AddressController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AddressController(SugarShopSalesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // لیست آدرس‌های کاربر
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var addresses = await _context.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.CreatedAt)
                .ToListAsync();
            return View(addresses);
        }

        // فرم ایجاد آدرس جدید (با قابلیت بازگشت)
        [HttpGet]
        public IActionResult Create(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Address model, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                model.UserId = userId!;
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;

                // اگر این اولین آدرس کاربر است، آن را به عنوان پیش‌فرض قرار بده
                var anyAddress = await _context.Addresses.AnyAsync(a => a.UserId == userId);
                if (!anyAddress)
                {
                    model.IsDefault = true;
                }

                _context.Addresses.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "آدرس با موفقیت اضافه شد.";

                // بازگشت به صفحه قبلی (مثلاً تسویه حساب) اگر returnUrl معتبر باشد
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction(nameof(Index));
            }
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        // فرم ویرایش آدرس
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) return NotFound();
            return View(address);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Address model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
                if (address == null) return NotFound();

                address.Title = model.Title;
                address.FullAddress = model.FullAddress;
                address.PostalCode = model.PostalCode;
                address.ReceiverName = model.ReceiverName;
                address.ReceiverPhone = model.ReceiverPhone;
                address.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "آدرس با موفقیت ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // حذف آدرس
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) return NotFound();

            if (address.IsDefault)
            {
                var another = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId && a.Id != id);
                if (another != null)
                {
                    another.IsDefault = true;
                }
            }

            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();
            TempData["Success"] = "آدرس با موفقیت حذف شد.";
            return RedirectToAction(nameof(Index));
        }

        // تنظیم آدرس به عنوان پیش‌فرض
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = _userManager.GetUserId(User);
            var addresses = await _context.Addresses.Where(a => a.UserId == userId).ToListAsync();
            foreach (var addr in addresses)
            {
                addr.IsDefault = (addr.Id == id);
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = "آدرس پیش‌فرض با موفقیت تغییر کرد.";
            return RedirectToAction(nameof(Index));
        }
    }
}