using System.Text.Json.Nodes;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.Api;

[TestFixture]
[NonParallelizable]
[Category("Api")]
public class BackendApiTests
{
    private IPlaywright _playwright = null!;
    private IAPIRequestContext _api = null!;

    [SetUp]
    public async Task SetUp()
    {
        _playwright = await Playwright.CreateAsync();
        _api = await _playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = TestSettings.BackendUrl
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_api != null)
        {
            await _api.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    [Test]
    public async Task WeatherForecast_ShouldReturnOkAndList()
    {
        var response = await _api.GetAsync("/WeatherForecast");

        Assert.That((int)response.Status, Is.EqualTo(200));

        var payload = await response.TextAsync();
        var json = JsonNode.Parse(payload) as JsonArray;

        Assert.That(json, Is.Not.Null);
        Assert.That(json!.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task RegisterAndLogin_ShouldReturnJwtToken()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"pw_{suffix}";
        var email = $"{username}@example.com";
        const string password = "pass1234";

        var registerResponse = await _api.PostAsync("/auth/register", new APIRequestContextOptions
        {
            DataObject = new
            {
                username,
                email,
                password
            }
        });

        Assert.That((int)registerResponse.Status, Is.EqualTo(200));
        var registerPayload = JsonNode.Parse(await registerResponse.TextAsync());
        Assert.That(registerPayload?["token"]?.ToString(), Is.Not.Empty);

        var loginResponse = await _api.PostAsync("/auth/login", new APIRequestContextOptions
        {
            DataObject = new
            {
                email,
                password
            }
        });

        Assert.That((int)loginResponse.Status, Is.EqualTo(200));
        var loginPayload = JsonNode.Parse(await loginResponse.TextAsync());
        Assert.That(loginPayload?["token"]?.ToString(), Is.Not.Empty);
    }

    [Test]
    public async Task ProtectedStats_WithoutToken_ShouldReturnUnauthorized()
    {
        var response = await _api.GetAsync("/player/me/stats");

        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"pw_wrong_{suffix}";
        var email = $"{username}@example.com";
        const string password = "pass1234";

        var registerResponse = await _api.PostAsync("/auth/register", new APIRequestContextOptions
        {
            DataObject = new
            {
                username,
                email,
                password
            }
        });

        Assert.That((int)registerResponse.Status, Is.EqualTo(200));

        var loginResponse = await _api.PostAsync("/auth/login", new APIRequestContextOptions
        {
            DataObject = new
            {
                email,
                password = "wrong-password"
            }
        });

        Assert.That((int)loginResponse.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task ProtectedStats_WithValidToken_ShouldReturnOk()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"pw_stats_{suffix}";
        var email = $"{username}@example.com";
        const string password = "pass1234";

        var registerResponse = await _api.PostAsync("/auth/register", new APIRequestContextOptions
        {
            DataObject = new
            {
                username,
                email,
                password
            }
        });

        Assert.That((int)registerResponse.Status, Is.EqualTo(200));
        var registerPayload = JsonNode.Parse(await registerResponse.TextAsync());
        var token = registerPayload?["token"]?.ToString();
        Assert.That(token, Is.Not.Null.And.Not.Empty);

        var authApi = await _playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = TestSettings.BackendUrl,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}"
            }
        });

        try
        {
            var response = await authApi.GetAsync("/player/me/stats");
            Assert.That((int)response.Status, Is.EqualTo(200));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task GlobalLeaderboard_WithoutRequiredQueryParams_ShouldReturnBadRequest()
    {
        var response = await _api.GetAsync("/api/leaderboard/global");
        Assert.That((int)response.Status, Is.EqualTo(400));
    }
}
