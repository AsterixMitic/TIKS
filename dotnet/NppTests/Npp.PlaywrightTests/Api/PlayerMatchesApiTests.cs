using System.Text.Json.Nodes;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.Api;

[TestFixture]
[NonParallelizable]
[Category("Api")]
public class PlayerMatchesApiTests
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
    public async Task MatchesByYear_WithToken_ShouldReturnOk()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.GetAsync("/api/playermatches/2026?page=1&limit=5");
            Assert.That((int)response.Status, Is.EqualTo(200));

            var payload = JsonNode.Parse(await response.TextAsync());
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload is JsonArray, Is.True);
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task MatchesByYear_WithoutToken_ShouldReturn401()
    {
        var response = await _api.GetAsync("/api/playermatches/2026");
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task MatchHistory_WithToken_ShouldReturnOk()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.GetAsync("/api/playermatches/history?page=1&limit=10");
            Assert.That((int)response.Status, Is.EqualTo(200));

            var payload = JsonNode.Parse(await response.TextAsync());
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload is JsonArray, Is.True);
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task MatchHistory_WithoutToken_ShouldReturn401()
    {
        var response = await _api.GetAsync("/api/playermatches/history");
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task MatchHistory_LimitCappedAt100()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            // Request with limit=200, server should cap at 100
            var response = await authApi.GetAsync("/api/playermatches/history?limit=200");
            Assert.That((int)response.Status, Is.EqualTo(200));

            var payload = JsonNode.Parse(await response.TextAsync()) as JsonArray;
            Assert.That(payload, Is.Not.Null);
            // Even if there are results, the count should not exceed 100
            Assert.That(payload!.Count, Is.LessThanOrEqualTo(100));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }
}
