using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SugarShop.Web.Services.Api
{
    /// <summary>
    /// کلید امضای JWT — یک منبع واحد برای هم تولیدکنندهٔ توکن و هم اعتبارسنج.
    /// اگر ApiAuth:SecretKey در appsettings تنظیم نشده باشد، در هر اجرای برنامه یک کلید
    /// تصادفی ساخته می‌شود؛ هر دو سمت از همین کلید استفاده می‌کنند (امن اما مخصوص یک پروسه).
    /// برای ماندگاری توکن‌ها بین restartها، کلید را در appsettings تنظیم کنید.
    /// </summary>
    public static class ApiAuthKeyHolder
    {
        private static string? _fallbackKey;

        public static string GetKey(IConfiguration config)
        {
            var configured = config["ApiAuth:SecretKey"];
            if (!string.IsNullOrWhiteSpace(configured) && configured.Length >= 32)
                return configured;

            _fallbackKey ??= Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
            return _fallbackKey;
        }

        /// <summary>آیا کلید واقعی (ماندگار) تنظیم شده است؟</summary>
        public static bool IsPersistentlyConfigured(IConfiguration config)
        {
            var configured = config["ApiAuth:SecretKey"];
            return !string.IsNullOrWhiteSpace(configured) && configured.Length >= 32;
        }
    }

    /// <summary>
    /// تولید توکن JWT برای کلاینت‌های موبایل API.
    /// </summary>
    public class ApiJwtTokenService
    {
        private readonly IConfiguration _config;

        public ApiJwtTokenService(IConfiguration config)
        {
            _config = config;
        }

        public bool IsConfigured => ApiAuthKeyHolder.GetKey(_config).Length >= 32;

        public (string token, DateTime expiresAt) CreateToken(string userId, string userName, string phone, IEnumerable<string> roles)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiAuthKeyHolder.GetKey(_config)));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var days = double.TryParse(_config["ApiAuth:TokenDays"], out var d) && d > 0 ? d : 30;
            var expires = DateTime.UtcNow.AddDays(days);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId),
                new(JwtRegisteredClaimNames.UniqueName, userName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new("phone", phone ?? "")
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var token = new JwtSecurityToken(
                issuer: _config["ApiAuth:Issuer"] ?? "SugarShop",
                audience: _config["ApiAuth:Audience"] ?? "SugarShopMobile",
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }
    }
}
