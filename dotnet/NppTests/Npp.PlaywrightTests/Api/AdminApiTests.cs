using Microsoft.Playwright;
using NUnit.Framework;

namespace Npp.PlaywrightTests.Api;

[TestFixture]
[NonParallelizable]
[Category("Api")]
public class AdminApiTests
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
    public async Task SnapshotLeaderboards_WithToken_ShouldReturnOk()
    {
        var (token, _) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.PostAsync("/api/admin/snapshot-leaderboards", new APIRequestContextOptions());
            Assert.That((int)response.Status, Is.EqualTo(200));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task SnapshotLeaderboards_WithoutToken_ShouldReturn401()
    {
        var response = await _api.PostAsync("/api/admin/snapshot-leaderboards", new APIRequestContextOptions());
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task AddPoints_WithValidData_ShouldReturnOk()
    {
        var (token, playerId) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.PostAsync("/api/admin/add-points", new APIRequestContextOptions
            {
                DataObject = new
                {
                    playerId,
                    points = 100,
                    wins = 1,
                    losses = 0
                }
            });
            Assert.That((int)response.Status, Is.EqualTo(200));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task AddPoints_WithoutToken_ShouldReturn401()
    {
        var response = await _api.PostAsync("/api/admin/add-points", new APIRequestContextOptions
        {
            DataObject = new
            {
                playerId = Guid.NewGuid().ToString(),
                points = 100,
                wins = 1,
                losses = 0
            }
        });
        Assert.That((int)response.Status, Is.EqualTo(401));
    }

    [Test]
    public async Task AddPoints_WithInvalidPoints_ShouldReturnBadRequest()
    {
        var (token, playerId) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.PostAsync("/api/admin/add-points", new APIRequestContextOptions
            {
                DataObject = new
                {
                    playerId,
                    points = 10001,
                    wins = 1,
                    losses = 0
                }
            });
            Assert.That((int)response.Status, Is.EqualTo(400));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }

    [Test]
    public async Task AddPoints_WithInvalidWins_ShouldReturnBadRequest()
    {
        var (token, playerId) = await ApiTestHelper.RegisterAndGetToken(_api);
        var authApi = await ApiTestHelper.CreateAuthenticatedContext(_playwright, token);

        try
        {
            var response = await authApi.PostAsync("/api/admin/add-points", new APIRequestContextOptions
            {
                DataObject = new
                {
                    playerId,
                    points = 100,
                    wins = 101,
                    losses = 0
                }
            });
            Assert.That((int)response.Status, Is.EqualTo(400));
        }
        finally
        {
            await authApi.DisposeAsync();
        }
    }
}
