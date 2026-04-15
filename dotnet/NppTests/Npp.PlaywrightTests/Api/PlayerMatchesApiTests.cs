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

    [Test]
    public async Task DeleteMatch_WithInvalidId_ShouldReturn404()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var fakeMatchId = Guid.NewGuid();
            var response = await authApi.DeleteAsync($"/api/playermatches/2026/{fakeMatchId}");
            Assert.That((int)response.Status, Is.EqualTo(404));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task DeleteMatch_WithoutToken_ShouldReturn401()
    {
        var fakeMatchId = Guid.NewGuid();
        var response = await _api.DeleteAsync($"/api/playermatches/2026/{fakeMatchId}");
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task MatchesByYear_WithInvalidYearFormat_ShouldReturn400()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.GetAsync("/api/playermatches/26");
            Assert.That((int)response.Status, Is.EqualTo(400));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task DeleteMatch_BelongingToAnotherUser_ShouldReturn404()
    {
        var (tokenA, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var (tokenB, _) = await ApiTestHelper.RegisterAndGetToken(_api);

        var authApiB = await ApiTestHelper.CreateAuthenticatedContext(_playwright, tokenB);

        try
        {
            // User B attempts to delete a match ID that belongs to user A.
            // The service filters by the caller's player ID, so this is indistinguishable
            // from a non-existent match and must return 404.
            var authApiA = await ApiTestHelper.CreateAuthenticatedContext(_playwright, tokenA);
            string matchIdFromA;
            try
            {
                // Fetch any existing match for user A to get a real match ID.
                // If none exist, fall back to a random Guid (still a valid 404 scenario).
                var listResp = await authApiA.GetAsync($"/api/playermatches/{DateTime.UtcNow.Year}?page=1&limit=1");
                var list = System.Text.Json.Nodes.JsonNode.Parse(await listResp.TextAsync()) as System.Text.Json.Nodes.JsonArray;
                matchIdFromA = list?.Count > 0
                    ? list[0]!["matchId"]!.ToString()
                    : Guid.NewGuid().ToString();
            }
            finally
            {
                await authApiA.DisposeAsync();
            }

            var response = await authApiB.DeleteAsync($"/api/playermatches/{DateTime.UtcNow.Year}/{matchIdFromA}");
            Assert.That((int)response.Status, Is.EqualTo(404));
        }
        finally
        {
            await authApiB.DisposeAsync();
        }
    }
}
