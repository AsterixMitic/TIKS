using System.Text.Json.Nodes;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.Api;

[TestFixture]
[NonParallelizable]
[Category("Api")]
public class LeaderboardApiTests
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
    public async Task GlobalLeaderboard_WithValidParams_ShouldReturnOk()
    {
        var response = await _api.GetAsync(
            "/api/leaderboard/global?periodType=MONTHLY&periodId=2026-02&limit=10");

        Assert.That((int)response.Status, Is.EqualTo(200));

        var payload = JsonNode.Parse(await response.TextAsync());
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!["periodType"]?.ToString(), Is.Not.Null);
        Assert.That(payload["entries"], Is.Not.Null);
        Assert.That(payload["entries"] is JsonArray, Is.True);
    }

    [Test]
    public async Task GlobalLeaderboard_WithoutParams_ShouldReturn400()
    {
        var response = await _api.GetAsync("/api/leaderboard/global");
        Assert.That((int)response.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task WinsLeaderboard_ShouldReturnOk()
    {
        var response = await _api.GetAsync(
            "/api/leaderboard/wins?category=most_wins&limit=10");

        Assert.That((int)response.Status, Is.EqualTo(200));

        var payload = JsonNode.Parse(await response.TextAsync());
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!["category"]?.ToString(), Is.Not.Null);
        Assert.That(payload["entries"], Is.Not.Null);
        Assert.That(payload["entries"] is JsonArray, Is.True);
    }

    [Test]
    public async Task PlayerStreak_WithValidId_ShouldReturnOk()
    {
        var (_, playerId) = await ApiTestHelper.RegisterAndGetToken(_api);

        var response = await _api.GetAsync($"/api/leaderboard/streak/{playerId}");

        // New player may not have streak data yet — accept 200 or 404
        Assert.That((int)response.Status, Is.AnyOf(200, 404));

        if (response.Status == 200)
        {
            var payload = JsonNode.Parse(await response.TextAsync());
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!["currentStreak"], Is.Not.Null);
            Assert.That(payload["longestStreak"], Is.Not.Null);
        }
    }

    [Test]
    public async Task PlayerStreak_WithInvalidId_ShouldReturn404or400()
    {
        var randomGuid = Guid.NewGuid().ToString();
        var response = await _api.GetAsync($"/api/leaderboard/streak/{randomGuid}");

        Assert.That((int)response.Status, Is.AnyOf(404, 400));
    }

    [Test]
    public async Task LongestStreak_ShouldReturnOk()
    {
        var response = await _api.GetAsync(
            "/api/leaderboard/longest-streak?category=global_all_time&limit=10");

        Assert.That((int)response.Status, Is.EqualTo(200));

        var payload = JsonNode.Parse(await response.TextAsync());
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!["category"]?.ToString(), Is.Not.Null);
        Assert.That(payload["entries"], Is.Not.Null);
        Assert.That(payload["entries"] is JsonArray, Is.True);
    }
}
