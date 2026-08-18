using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Web.Controllers
{
    public class SetupController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SugarShopIdentityDbContext _identityDb;

        public SetupController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SugarShopIdentityDbContext identityDb)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _identityDb = identityDb;
        }

        [HttpGet]
        public async Task<IActionResult> CreateAdmin()
        {
            var result = new List<string>();

            // اطمینان از ایجاد دیتابیس
            await _identityDb.Database.EnsureCreatedAsync();

            // ایجاد نقش Admin
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
                result.Add("✅ نقش Admin ایجاد شد.");
            }
            else
            {
                result.Add("⚠️ نقش Admin قبلاً وجود داشت.");
            }

            // حذف کاربر admin قبلی اگر وجود داشت
            var existingUser = await _userManager.FindByNameAsync("admin");
            if (existingUser != null)
            {
                await _userManager.DeleteAsync(existingUser);
                result.Add("⚠️ کاربر admin قبلی حذف شد.");
            }

            // ایجاد کاربر جدید
            var user = new IdentityUser
            {
                UserName = "admin",
                Email = "admin@sugarshop.com",
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, "Admin@123");

            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                result.Add("✅ کاربر admin با موفقیت ایجاد شد.");
                result.Add("🔐 نام کاربری: admin");
                result.Add("🔐 رمز عبور: Admin@123");
            }
            else
            {
                foreach (var error in createResult.Errors)
                {
                    result.Add($"❌ خطا: {error.Description}");
                }
            }

            ViewBag.Messages = result;
            return View();
        }
    }
}