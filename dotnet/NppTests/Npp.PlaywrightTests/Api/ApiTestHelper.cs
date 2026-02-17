using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace Npp.PlaywrightTests.Api;

public static class ApiTestHelper
{
    public static async Task<(string token, string playerId)> RegisterAndGetToken(IAPIRequestContext api)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"pw_{suffix}";
        var email = $"{username}@example.com";
        const string password = "pass1234";

        var response = await api.PostAsync("/auth/register", new APIRequestContextOptions
        {
            DataObject = new
            {
                username,
                email,
                password
            }
        });

        if (response.Status != 200)
        {
            throw new InvalidOperationException(
                $"Registration failed with status {response.Status}: {await response.TextAsync()}");
        }

        var payload = JsonNode.Parse(await response.TextAsync());
        var token = payload?["token"]?.ToString()
                    ?? throw new InvalidOperationException("Token missing from register response");
        var playerId = payload["playerId"]?.ToString()
                       ?? throw new InvalidOperationException("PlayerId missing from register response");

        return (token, playerId);
    }

    public static async Task<IAPIRequestContext> CreateAuthenticatedContext(IPlaywright playwright, string token)
    {
        return await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = TestSettings.BackendUrl,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}"
            }
        });
    }
}
