using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NppApi.ComponentTests.TestHelpers;

internal static class ControllerTestContextFactory
{
    public static void SetUser(ControllerBase controller, Guid? playerId = null, string? rawPlayerId = null)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrWhiteSpace(rawPlayerId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, rawPlayerId));
        }
        else if (playerId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, playerId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }
}
