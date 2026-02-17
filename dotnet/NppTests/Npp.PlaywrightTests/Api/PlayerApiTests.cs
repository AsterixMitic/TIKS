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
}
