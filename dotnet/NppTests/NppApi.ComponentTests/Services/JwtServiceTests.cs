using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using NppCore.Services.Features.Auth;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class JwtServiceTests
{
    private JwtService CreateService(string? secretKey, string? issuer = null, string? audience = null)
    {
        var configData = new Dictionary<string, string?>();
        if (secretKey != null) configData["Jwt:SecretKey"] = secretKey;
        if (issuer != null) configData["Jwt:Issuer"] = issuer;
        if (audience != null) configData["Jwt:Audience"] = audience;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return new JwtService(configuration);
    }

    [Test]
    public void GenerateToken_WithValidConfig_ReturnsValidJwt()
    {
        var service = CreateService("this-is-a-secret-key-that-is-at-least-32-chars");
        var playerId = Guid.NewGuid();

        var token = service.GenerateToken(playerId, "testuser", "test@example.com");

        Assert.That(token, Is.Not.Null.And.Not.Empty);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Multiple(() =>
        {
            Assert.That(jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value,
                Is.EqualTo(playerId.ToString()));
            Assert.That(jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value,
                Is.EqualTo("testuser"));
            Assert.That(jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value,
                Is.EqualTo("test@example.com"));
        });
    }

    [Test]
    public void GenerateToken_TokenContainsCorrectExpiry()
    {
        var service = CreateService("this-is-a-secret-key-that-is-at-least-32-chars");

        var token = service.GenerateToken(Guid.NewGuid(), "user", "user@example.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expectedExpiry = DateTime.UtcNow.AddDays(7);
        Assert.That(jwt.ValidTo, Is.EqualTo(expectedExpiry).Within(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void GenerateToken_WithoutSecretKey_Throws()
    {
        var service = CreateService(null);

        Assert.Throws<InvalidOperationException>(
            () => service.GenerateToken(Guid.NewGuid(), "user", "user@example.com"));
    }
}
