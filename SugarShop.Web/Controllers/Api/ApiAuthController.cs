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
        private readonly SmsService _sms;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApiJwtTokenService _jwt;

        public ApiAuthController(SmsService sms, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ApiJwtTokenService jwt)
        {
            _sms = sms;
            _userManager = userManager;
            _roleManager = roleManager;
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
