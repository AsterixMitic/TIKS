using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.Api;

[TestFixture]
[NonParallelizable]
[Category("Api")]
public class PlayerApiTests
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
        if (_api != null) await _api.DisposeAsync();
        _playwright?.Dispose();
    }

    [Test]
    public async Task PlayerStats_WithToken_ShouldReturnCorrectStructure()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.GetAsync("/player/me/stats");
            Assert.That((int)response.Status, Is.EqualTo(200));

            var payload = JsonNode.Parse(await response.TextAsync());
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!["totalPoints"]?.GetValue<long>(), Is.EqualTo(0));
            Assert.That(payload["wins"]?.GetValue<long>(), Is.EqualTo(0));
            Assert.That(payload["losses"]?.GetValue<long>(), Is.EqualTo(0));
            Assert.That(payload["winRate"]?.ToString(), Is.EqualTo("0%"));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task PlayerStats_WithoutToken_ShouldReturn401()
    {
        var response = await _api.GetAsync("/player/me/stats");
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task GetProfile_WithToken_ShouldReturnCorrectFields()
    {
        var (token, playerId) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.GetAsync("/player/me");
            Assert.That((int)response.Status, Is.EqualTo(200));

            var payload = JsonNode.Parse(await response.TextAsync());
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!["playerId"]?.ToString(), Is.EqualTo(playerId));
            Assert.That(payload["username"]?.ToString(), Is.Not.Null.And.Not.Empty);
            Assert.That(payload["email"]?.ToString(), Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task GetProfile_WithoutToken_ShouldReturn401()
    {
        var response = await _api.GetAsync("/player/me");
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task UpdateProfile_WithToken_ShouldReturn200WithUpdatedUsername()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var newUsername = $"updated_{Guid.NewGuid().ToString("N")[..8]}";
            var body = JsonSerializer.Serialize(new { username = newUsername });

            var response = await authApi.PutAsync("/player/me", new APIRequestContextOptions
            {
                DataString = body,
                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
            });

            Assert.That((int)response.Status, Is.EqualTo(200));

            var payload = JsonNode.Parse(await response.TextAsync());
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!["username"]?.ToString(), Is.EqualTo(newUsername));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task UpdateProfile_WithoutToken_ShouldReturn401()
    {
        var body = JsonSerializer.Serialize(new { username = "anyname" });
        var response = await _api.PutAsync("/player/me", new APIRequestContextOptions
        {
            DataString = body,
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
        });
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task DeleteAccount_WithToken_ShouldReturn204()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.DeleteAsync("/player/me");
            Assert.That((int)response.Status, Is.EqualTo(204));

            var verifyResponse = await authApi.GetAsync("/player/me");
            Assert.That((int)verifyResponse.Status, Is.EqualTo(404));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task UpdateProfile_WithTakenUsername_ShouldReturn409()
    {
        // Register user A to occupy their username
        var (tokenA, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApiA = await ApiTestHelper.CreateAuthenticatedContext(_playwright, tokenA);
        string takenUsername;
        try
        {
            var profileResp = await authApiA.GetAsync("/player/me");
            var profile = JsonNode.Parse(await profileResp.TextAsync());
            takenUsername = profile!["username"]!.ToString();
        }
        finally
        {
            await authApiA.DisposeAsync();
        }

        // Register user B and try to claim user A's username
        var (tokenB, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApiB = await ApiTestHelper.CreateAuthenticatedContext(_playwright, tokenB);
        try
        {
            var body = JsonSerializer.Serialize(new { username = takenUsername });
            var response = await authApiB.PutAsync("/player/me", new APIRequestContextOptions
            {
                DataString = body,
                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
            });
            Assert.That((int)response.Status, Is.EqualTo(409));
        }
        finally
        {
            await authApiB.DisposeAsync();
        }
    }
}
