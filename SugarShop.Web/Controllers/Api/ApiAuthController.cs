using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Web.Services.Api;
using SugarShop.Web.Services.Sms;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SugarShop.Web.Controllers.Api
{
    /// <summary>
    /// احراز هویت موبایل — همان OTP پیامکی سایت، خروجی JWT.
    /// حساب کاربری کاملاً مشترک با سایت است (یک حساب، دو کانال).
    /// </summary>
    [ApiController]
    [Route("api/v1/auth")]
    public class ApiAuthController : ControllerBase
    {
        private const string BiometricCookieName = "SugarShopBiometric";
        private const string BiometricSessionPath = "/api/v1/auth/biometric-session";
        private readonly SmsService _sms;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApiJwtTokenService _jwt;

        public ApiAuthController(SmsService sms, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager,
            ApiJwtTokenService jwt)
        {
            _sms = sms;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _jwt = jwt;
        }

        public class OtpRequestDto { public string? Phone { get; set; } }
        public class OtpVerifyDto { public string? Phone { get; set; } public string? Code { get; set; } public string? FullName { get; set; } }

        /// <summary>درخواست کد ورود (ارسال پیامک OTP)</summary>
        [HttpPost("otp/request")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestOtp([FromBody] OtpRequestDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (ok, message) = await _sms.SendOtpAsync(dto.Phone ?? "", ip, "Login");
            if (!ok) return BadRequest(new { success = false, message });

            var normalized = SmsService.NormalizePhone(dto.Phone ?? "");
            var exists = await _userManager.Users.AnyAsync(u => u.PhoneNumber == normalized);
            return Ok(new { success = true, message, isNewUser = !exists });
        }

        /// <summary>بررسی کد و دریافت توکن JWT</summary>
        [HttpPost("otp/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyDto dto)
        {
            if (!_jwt.IsConfigured)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { success = false, message = "ورود اپلیکیشن موقتاً پیکربندی نشده است." });

            var normalized = SmsService.NormalizePhone(dto.Phone ?? "");
            if (!SmsService.IsValidIranMobile(normalized))
                return BadRequest(new { success = false, message = "شماره موبایل معتبر نیست." });

            if (!await _sms.ValidateOtpAsync(normalized, dto.Code ?? "", "Login"))
                return BadRequest(new { success = false, message = "کد وارد شده صحیح نیست یا منقضی شده است." });

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalized);
            if (user == null)
            {
                // ثبت‌نام خودکار — دقیقاً همان قواعد سایت
                var autoName = string.IsNullOrWhiteSpace(dto.FullName) ? "مشتری " + normalized[^4..] : dto.FullName.Trim();
                user = new ApplicationUser
                {
                    UserName = "u" + normalized,
                    PhoneNumber = normalized,
                    PhoneNumberConfirmed = true,
                    FullName = autoName,
                    Email = normalized + "@phone.local"
                };
                var create = await _userManager.CreateAsync(user);
                if (!create.Succeeded)
                    return BadRequest(new { success = false, message = string.Join(" | ", create.Errors.Select(e => e.Description)) });
                if (!await _roleManager.RoleExistsAsync("User"))
                    await _roleManager.CreateAsync(new IdentityRole("User"));
                await _userManager.AddToRoleAsync(user, "User");
            }
            else if (await _userManager.IsLockedOutAsync(user))
            {
                return Unauthorized(new { success = false, message = "حساب کاربری موقتاً قفل شده است." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var (token, expires) = _jwt.CreateToken(user.Id, user.UserName!, user.PhoneNumber ?? "", roles);
            return Ok(new
            {
                success = true,
                token,
                expiresAtUtc = expires,
                user = new { user.Id, user.UserName, user.FullName, user.PhoneNumber, user.AvatarPath, roles }
            });
        }

        /// <summary>صدور بلیت نشست موبایل فقط برای حسابی که همین حالا با کوکی سایت احراز هویت شده است.</summary>
        [HttpPost("biometric-token")]
        [Authorize]
        public async Task<IActionResult> CreateBiometricToken()
        {
            if (!_jwt.IsConfigured)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { success = false, message = "ورود بیومتریک موقتاً پیکربندی نشده است." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null || await _userManager.IsLockedOutAsync(user)
                || string.IsNullOrWhiteSpace(user.SecurityStamp)) return Forbid();

            var roles = await _userManager.GetRolesAsync(user);
            var (token, expires) = _jwt.CreateToken(user.Id, user.UserName!, user.PhoneNumber ?? "", roles,
                "native_biometric_session", user.SecurityStamp);
            SetBiometricCookie(token, expires);
            return Ok(new
            {
                success = true,
                expiresAtUnixMs = new DateTimeOffset(expires).ToUnixTimeMilliseconds()
            });
        }

        /// <summary>پاک‌کردن نشست بیومتریک از این WebView هنگام خاموش‌کردن قابلیت.</summary>
        [HttpDelete("biometric-token")]
        [Authorize]
        public IActionResult ClearBiometricToken()
        {
            Response.Cookies.Delete(BiometricCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Path = BiometricSessionPath
            });
            return Ok(new { success = true });
        }

        /// <summary>تبدیل امن بلیت بیومتریک به کوکی Identity داخل WebView؛ فقط توکن ویژهٔ اپ پذیرفته می‌شود.</summary>
        [HttpGet("biometric-session")]
        [Authorize(AuthenticationSchemes = "ApiJwt")]
        public async Task<IActionResult> RestoreBiometricSession()
        {
            if (!_jwt.IsConfigured || User.FindFirstValue("token_use") != "native_biometric_session")
                return Forbid();

            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                         ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId ?? "");
            var tokenStamp = User.FindFirstValue("security_stamp");
            if (user == null || await _userManager.IsLockedOutAsync(user)
                || string.IsNullOrWhiteSpace(tokenStamp)
                || !string.Equals(tokenStamp, user.SecurityStamp, StringComparison.Ordinal))
                return Forbid();

            await _signInManager.SignInAsync(user, isPersistent: true);
            return LocalRedirect("/");
        }

        private void SetBiometricCookie(string token, DateTime expires)
        {
            Response.Cookies.Append(BiometricCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Expires = new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc)),
                Path = BiometricSessionPath
            });
        }

        /// <summary>اطلاعات کاربر جاری</summary>
        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = "ApiJwt")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                         ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId ?? "");
            if (user == null) return NotFound(new { success = false, message = "کاربر یافت نشد." });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { success = true, user = new { user.Id, user.UserName, user.FullName, user.PhoneNumber, user.AvatarPath, roles } });
        }
    }
}
