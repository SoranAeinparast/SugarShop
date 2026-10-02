using Microsoft.Extensions.Configuration;
using SugarShop.Web.Services.Api;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace SugarShop.Web.Tests;

public sealed class ApiJwtTokenServiceTests
{
    [Fact]
    public void Biometric_session_token_contains_user_and_security_stamp_claims()
    {
        var service = new ApiJwtTokenService(CreateConfiguration());

        var (token, _) = service.CreateToken(
            "user-123", "sugar-user", "09120000000", new[] { "User" },
            "native_biometric_session", "stamp-current");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("user-123", jwt.Subject);
        Assert.Contains(jwt.Claims, claim => claim.Type == "token_use"
            && claim.Value == "native_biometric_session");
        Assert.Contains(jwt.Claims, claim => claim.Type == "security_stamp"
            && claim.Value == "stamp-current");
    }

    [Fact]
    public void Token_creation_is_disabled_without_a_persistent_signing_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var service = new ApiJwtTokenService(configuration);

        Assert.False(service.IsConfigured);
        Assert.Throws<InvalidOperationException>(() => service.CreateToken(
            "user-123", "sugar-user", "09120000000", Array.Empty<string>()));
    }

    private static IConfiguration CreateConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiAuth:SecretKey"] = new string('s', 48),
                ["ApiAuth:Issuer"] = "SugarShop.Tests",
                ["ApiAuth:Audience"] = "SugarShop.Tests.Mobile"
            })
            .Build();
}
