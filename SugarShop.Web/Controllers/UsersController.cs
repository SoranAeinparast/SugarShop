using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    // ✅ فقط Admin و Owner به این بخش دسترسی دارند
    [Authorize(Roles = "Admin,Owner")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SugarShopIdentityDbContext _context;

        // ✅ نام کاربری Owner به صورت ثابت تعریف می‌شود
        private const string OwnerUserName = "SoransoftOWNER";

        public UsersController(UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SugarShopIdentityDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // ✅ فیلتر کردن کاربر Owner از لیست نمایش
            var users = await _userManager.Users
                .Where(u => u.UserName != OwnerUserName)
                .ToListAsync();

            // ✅ رفع N+1: یک کوئری برای همه نقش‌ها
            var userIds = users.Select(u => u.Id).ToList();
            var userRoles = await _context.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_context.Roles,
                      ur => ur.RoleId,
                      r => r.Id,
                      (ur, r) => new { ur.UserId, RoleName = r.Name })
                .ToListAsync();

            var userRolesDict = userRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

            ViewBag.UserRoles = userRolesDict;
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);

            // ✅ جلوگیری مطلق از دسترسی به صفحه ویرایش کاربر Owner
            if (user == null || user.UserName == OwnerUserName) return NotFound();

            // ✅ حذف نقش "Owner" از لیست نقش‌های قابل انتخاب در فرم
            var allRoles = await _roleManager.Roles
                .Where(r => r.Name != "Owner")
                .Select(r => r.Name)
                .ToListAsync();

            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.AllRoles = allRoles;
            ViewBag.UserRoles = userRoles;
            ViewBag.UserId = user.Id;
            ViewBag.UserName = user.UserName;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, List<string> selectedRoles)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);

            // ✅ جلوگیری مجدد از هرگونه تغییر روی کاربر Owner
            if (user == null || user.UserName == OwnerUserName) return NotFound();

            // ✅ اطمینان از اینکه نقش Owner به هیچ کاربر دیگری از طریق فرم داده نمی‌شود
            if (selectedRoles != null && selectedRoles.Contains("Owner"))
            {
                selectedRoles.Remove("Owner");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Except(selectedRoles ?? new List<string>()).ToList();
            var rolesToAdd = (selectedRoles ?? new List<string>()).Except(currentRoles).ToList();

            if (rolesToRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }
            if (rolesToAdd.Any())
            {
                await _userManager.AddToRolesAsync(user, rolesToAdd);
            }

            TempData["Success"] = "نقش‌های کاربر با موفقیت به‌روزرسانی شد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);

            // ✅ جلوگیری مطلق از حذف کاربر Owner (حتی توسط خودش یا ادمین)
            if (user == null || user.UserName == OwnerUserName)
            {
                TempData["Error"] = "این عملیات مجاز نیست.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
                TempData["Success"] = "کاربر با موفقیت حذف شد.";
            else
                TempData["Error"] = "خطا در حذف کاربر";

            return RedirectToAction(nameof(Index));
        }
    }
}