using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.ViewModels.Account;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly SugarShopSalesDbContext _salesDbContext;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            SugarShopSalesDbContext salesDbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _salesDbContext = salesDbContext;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var siteSettings = await _salesDbContext.SiteSettings.FirstOrDefaultAsync();
            ViewBag.SiteTitle = siteSettings?.SiteTitle ?? "شیرینی سرا ...";
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;
            if (!ModelState.IsValid)
                return Json(new { success = false, type = "error", message = GetModelErrors() });

            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null) user = await _userManager.FindByEmailAsync(model.Username);

            if (user == null)
                return Json(new { success = false, type = "error", message = "نام کاربری یا رمز عبور اشتباه است." });

            bool isOwner = await _userManager.IsInRoleAsync(user, "Owner");
            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            bool isOrderManager = await _userManager.IsInRoleAsync(user, "OrderManager");

            // ✅ اصلاح حیاتی: اگر کاربر Owner باشد، حتی اگر تیک "مرا به خاطر بسپار" زده باشد، کوکی ذخیره نشود
            bool isPersistent = model.RememberMe && !isAdmin && !isOrderManager && !isOwner;

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (isAdmin || isOrderManager || isOwner)
                {
                    return Json(new { success = true, redirectUrl = Url.Action("Index", "Admin") });
                }
                return Json(new { success = true, redirectUrl = model.ReturnUrl ?? Url.Action("Index", "Home") });
            }

            if (result.IsLockedOut)
                return Json(new { success = false, type = "warning", message = "حساب شما قفل شده است." });

            return Json(new { success = false, type = "error", message = "نام کاربری یا رمز عبور اشتباه است." });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, type = "error", message = GetModelErrors() });
            if (await _userManager.FindByNameAsync(model.Username) != null)
                return Json(new { success = false, type = "error", message = "این نام کاربری قبلاً ثبت شده است." });
            if (await _userManager.FindByEmailAsync(model.Email) != null)
                return Json(new { success = false, type = "error", message = "این ایمیل قبلاً ثبت شده است." });

            var user = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                FullName = model.FullName
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("User"))
                    await _roleManager.CreateAsync(new IdentityRole("User"));
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return Json(new { success = true, type = "success", message = "ثبت‌نام با موفقیت انجام شد!", redirectUrl = Url.Action("Index", "Home") });
            }
            return Json(new { success = false, type = "error", message = string.Join(" | ", result.Errors.Select(e => e.Description)) });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
                return Json(new { success = false, type = "error", message = "ایمیل الزامی است." });

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return Json(new { success = true, type = "info", message = "اگر ایمیل وارد شده در سیستم ثبت شده باشد، لینک بازیابی رمز عبور به آن ارسال خواهد شد." });
            }

            var siteSettings = await _salesDbContext.SiteSettings.FirstOrDefaultAsync();
            string siteTitle = string.IsNullOrWhiteSpace(siteSettings?.SiteTitle) ? "شیرینی سرا ..." : siteSettings.SiteTitle;

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = encodedCode }, protocol: Request.Scheme);

            var htmlMessage = $@"
<div style='font-family: Tahoma, Arial; direction: rtl; text-align: right; padding: 20px;'>
    <h2 style='color: #d4a056;'>بازیابی رمز عبور - {siteTitle}</h2>
    <p>سلام {user.FullName} عزیز،</p>
    <p>شما درخواست بازیابی رمز عبور خود را داده‌اید.</p>
    <p>برای تغییر رمز عبور، روی دکمه زیر کلیک کنید:</p>
    <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='background-color: #d4a056; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block; margin-top: 10px;'>تغییر رمز عبور</a>
    <p style='color: #666; font-size: 12px; margin-top: 20px;'>اگر شما این درخواست را نداده‌اید، این ایمیل را نادیده بگیرید.</p>
</div>";

            try
            {
                await _emailSender.SendEmailAsync(email, $"بازیابی رمز عبور - {siteTitle}", htmlMessage);
                return Json(new { success = true, type = "success", message = "لینک بازیابی رمز عبور به ایمیل شما ارسال شد. لطفاً صندوق ورودی (و پوشه اسپم) را بررسی کنید." });
            }
            catch
            {
                return Json(new { success = false, type = "error", message = "خطا در ارسال ایمیل. لطفاً با پشتیبانی تماس بگیرید." });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string? code, string? userId)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Login");
            return View(new ResetPasswordViewModel { Code = code, UserId = userId });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, type = "error", message = GetModelErrors() });
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return Json(new { success = false, type = "error", message = "کاربر یافت نشد." });

            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
            var result = await _userManager.ResetPasswordAsync(user, code, model.Password);
            if (result.Succeeded)
            {
                return Json(new { success = true, type = "success", message = "رمز عبور با موفقیت تغییر کرد.", redirectUrl = Url.Action("Login") });
            }
            return Json(new { success = false, type = "error", message = string.Join(" | ", result.Errors.Select(e => e.Description)) });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
        private string GetModelErrors() => string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
    }
}