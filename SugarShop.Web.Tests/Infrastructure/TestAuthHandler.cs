using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SugarShop.Web.Tests.Infrastructure;

/// <summary>
/// احراز هویت آزمون: بدون کوکی، بدون دیتابیس هویتی و بدون هیچ ورودی خارجی، یک کاربر
/// تثبیتی برمی‌گرداند که همه‌ی نقش‌ها را دارد. فقط برای اینکه صفحه‌های پشت ورود
/// (پروفایل/پنل مدیریت/سرآشپز) هم در آزمون جداسازی اپ و وب رندر شوند.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    /// <summary>شناسه‌ی همان کاربری که در دیتابیس in-memory آزمون ساخته می‌شود.</summary>
    public const string UserId = "11111111-2222-3333-4444-555555555555";

    public const string FullName = "کاربر آزمون";

    /// <summary>همه‌ی نقش‌های کارمندی، تا هر صفحه‌ی پشت <c>[Authorize(Roles=...)]</c> قابل آزمون باشد.</summary>
    public static readonly string[] Roles = { "User", "Admin", "Owner", "OrderManager", "Chef" };

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId),
            new(ClaimTypes.Name, FullName)
        };
        claims.AddRange(Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // ⚠️ نکته‌ی مهم: نوع احراز هویت این Identity باید همان اسکیم اصلی Identity باشد
        // ("Identity.Application")، وگرنه SignInManager.IsSignedIn در سمت برنامه false می‌شود و
        // صفحه‌ها فکر می‌کنند کاربر وارد نشده است — دقیقاً همان‌طور که با کوکی واقعی می‌شود.
        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
