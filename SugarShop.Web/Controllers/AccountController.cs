using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sms;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services.Sms;
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
        private readonly SmsService _sms;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender,
            SugarShopSalesDbContext salesDbContext,
            SmsService sms)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _salesDbContext = salesDbContext;
            _sms = sms;
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
            bool isChef = await _userManager.IsInRoleAsync(user, "Chef");

            // ✅ اصلاح حیاتی: اگر کاربر کارمند (Owner/Admin/OrderManager/Chef) باشد، حتی اگر تیک "مرا به خاطر بسپار" زده باشد، کوکی ذخیره نشود
            bool isPersistent = model.RememberMe && !isAdmin && !isOrderManager && !isOwner && !isChef;

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, isPersistent, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (isAdmin || isOrderManager || isOwner)
                {
                    return Json(new { success = true, redirectUrl = Url.Action("Index", "Admin") });
                }
                if (isChef)
                {
                    // سرآشپز مستقیم به داشبورد مخصوص خود هدایت می‌شود
                    return Json(new { success = true, redirectUrl = Url.Action("Index", "Chef") });
                }
                // کاربر عادی هرگز به صفحات مدیریتی هدایت نمی‌شود (جلوگیری از صفحه «دسترسی ممنوع»)
                return Json(new { success = true, redirectUrl = SafeRedirect(model.ReturnUrl) });
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

        // ═══════════ ورود / ثبت‌نام با شماره موبایل + کد یکبار مصرف (OTP) ═══════════

        /// <summary>درخواست کد OTP برای ورود/ثبت‌نام. اگر شماره ثبت‌نشده باشد، حساب در همان لحظه ساخته می‌شود.</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestOtp(string phone)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (ok, message) = await _sms.SendOtpAsync(phone, ip, "Login");
            if (!ok) return Json(new { success = false, message });

            // شماره نرمال‌شده به کلاینت برمی‌گردد تا مرحله بعد با همان کار کند
            var normalized = SmsService.NormalizePhone(phone);
            var exists = await _userManager.Users.AnyAsync(u => u.PhoneNumber == normalized);
            return Json(new
            {
                success = true,
                message,
                isNew = !exists,
                maskedPhone = MaskPhone(normalized)
            });
        }

        /// <summary>بررسی کد OTP و ورود (یا ساخت حساب و ورود).</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string phone, string code, string? fullName, string? returnUrl)
        {
            var normalized = SmsService.NormalizePhone(phone);
            if (!SmsService.IsValidIranMobile(normalized))
                return Json(new { success = false, message = "شماره موبایل معتبر نیست." });

            var matching = await _userManager.Users.Where(u => u.PhoneNumber == normalized).ToListAsync();
            ApplicationUser? user;
            if (matching.Count <= 1)
            {
                user = matching.FirstOrDefault();
            }
            else
            {
                // چند حساب با یک شماره (مثلاً حساب مشتری + حساب کارمندی مالک با همان شماره):
                // حساب کارمندی مقدم است تا ورود با کد همیشه قطعی و قابل پیش‌بینی باشد.
                user = null;
                foreach (var role in new[] { "Owner", "Admin", "OrderManager", "Chef" })
                {
                    var staffIds = (await _userManager.GetUsersInRoleAsync(role)).Select(u => u.Id).ToHashSet();
                    user = matching.FirstOrDefault(u => staffIds.Contains(u.Id));
                    if (user != null) break;
                }
                user ??= matching[0];
            }

            bool isNewUser = user == null;
            // برای کاربر جدید نام اجباری است؛ این بررسی باید پیش از مصرف کد انجام شود
            // تا کاربر بتواند بعد از وارد کردن نام، با همان کد وارد شود.
            if (isNewUser && string.IsNullOrWhiteSpace(fullName))
                return Json(new { success = false, message = "لطفاً نام و نام خانوادگی خود را وارد کنید تا حساب ساخته شود." });

            if (!await _sms.ValidateOtpAsync(normalized, code, "Login"))
                return Json(new { success = false, message = "کد وارد شده صحیح نیست یا منقضی شده است." });

            if (user == null)
            {
                // ثبت‌نام خودکار با شماره موبایل — نام از کاربر گرفته می‌شود، ایمیل و نام کاربری بعداً در پروفایل قابل تغییرند
                var autoName = fullName!.Trim();
                user = new ApplicationUser
                {
                    UserName = "u" + normalized,          // نام کاربری یکتا بر اساس شماره (در پروفایل قابل تغییر)
                    PhoneNumber = normalized,
                    PhoneNumberConfirmed = true,
                    FullName = autoName,
                    Email = normalized + "@phone.local"   // ایمیل الزامی Identity؛ در پروفایل قابل تغییر
                };
                var create = await _userManager.CreateAsync(user);
                if (!create.Succeeded)
                    return Json(new { success = false, message = string.Join(" | ", create.Errors.Select(e => e.Description)) });

                if (!await _roleManager.RoleExistsAsync("User"))
                    await _roleManager.CreateAsync(new IdentityRole("User"));
                await _userManager.AddToRoleAsync(user, "User");

                await _sms.SendScenarioAsync(normalized, SmsScenario.RegisterWelcome,
                    new Dictionary<string, string> { { "CustomerName", autoName.Split(' ')[0] } });
            }

            // ورود با کد یکبار مصرف: کوکی ماندگار ۱۴ روزه برای مشتریان — تا با هر بار باز کردن اپ
            // (حتی پس از تأیید اثر انگشت) کاربر همچنان لاگین باشد. کارکنان را همان
            // سیاست موجود در Program.cs به جلسه‌ی غیرماندگار محدود می‌کند.
            var signInProps = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            };
            await _signInManager.SignInAsync(user, signInProps);

            bool isStaff = await _userManager.IsInRoleAsync(user, "Owner")
                        || await _userManager.IsInRoleAsync(user, "Admin")
                        || await _userManager.IsInRoleAsync(user, "OrderManager")
                        || await _userManager.IsInRoleAsync(user, "Chef");
            var redirect = isStaff ? Url.Action("Index", "Admin")
                : SafeRedirect(returnUrl);

            return Json(new { success = true, redirectUrl = redirect, isNew = isNewUser });
        }

        /// <summary>ارسال کد بازیابی رمز عبور با پیامک.</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPasswordBySms(string phone)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (ok, message) = await _sms.SendOtpAsync(phone, ip, "PasswordReset");
            if (!ok) return Json(new { success = false, message });

            var normalized = SmsService.NormalizePhone(phone);
            return Json(new { success = true, message, phone = normalized });
        }

        /// <summary>بررسی کد بازیابی و ورود موقت کاربر برای تغییر رمز.</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyResetOtp(string phone, string code)
        {
            var normalized = SmsService.NormalizePhone(phone);
            if (!await _sms.ValidateOtpAsync(normalized, code, "PasswordReset"))
                return Json(new { success = false, message = "کد وارد شده صحیح نیست یا منقضی شده است." });

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalized);
            if (user == null)
                return Json(new { success = false, message = "کاربری با این شماره یافت نشد." });

            // ورود موقت؛ کاربر باید رمز را در ادامه تغییر دهد
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Json(new { success = true, redirectUrl = Url.Action("ChangePassword", "Profile") });
        }

        private static string MaskPhone(string phone)
            => phone.Length == 11 ? phone[..4] + "***" + phone[7..] : phone;

        /// <summary>
        /// هدایت امن پس از ورود: کاربر عادی فقط به صفحات عمومی هدایت می‌شود؛
        /// آدرس‌های مدیریتی (که برای او «دسترسی ممنوع» می‌دهند) نادیده گرفته و به صفحه اصلی می‌رود.
        /// </summary>
        private static readonly string[] AdminOnlyPaths =
        {
            "/admin", "/smssettings", "/chef", "/discountcodes", "/users", "/tickets",
            "/wallet", "/newsletter", "/menu", "/paymentgateways", "/products", "/sweetitems",
            "/categories", "/boxtypes", "/sliders", "/areas/"
        };

        private static string SafeRedirect(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl)) return "/";
            if (!returnUrl.StartsWith("/") || returnUrl.StartsWith("//")) return "/";
            var path = returnUrl.Split('?')[0].ToLowerInvariant();
            if (AdminOnlyPaths.Any(p => path.StartsWith(p))) return "/";
            return returnUrl;
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
    <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}' style='background-color: #d4a056; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block; margin-top: 10px;'>تغییر رمز عبور</a>
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
            var user = await _userManager.FindByIdAsync(model.UserId!);
            if (user == null) return Json(new { success = false, type = "error", message = "کاربر یافت نشد." });

            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code!));
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