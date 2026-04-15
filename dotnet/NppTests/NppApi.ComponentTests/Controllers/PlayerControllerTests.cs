using Microsoft.AspNetCore.Mvc;
using Moq;
using NppApi.ComponentTests.TestHelpers;
using NppApi.Controllers;
using NppCore.Models;
using NppCore.Services.Features.Player;
using NUnit.Framework;

namespace NppApi.ComponentTests.Controllers;

[TestFixture]
public class PlayerControllerTests
{
    private Mock<IPlayerStatsService> _statsServiceMock = null!;
    private Mock<IPlayerService> _playerServiceMock = null!;
    private PlayerController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _statsServiceMock = new Mock<IPlayerStatsService>();
        _playerServiceMock = new Mock<IPlayerService>();
        _controller = new PlayerController(_statsServiceMock.Object, _playerServiceMock.Object);
    }

    // ==================== GetMyStats ====================

    [Test]
    public async Task GetMyStats_WhenPlayerIdClaimMissing_ReturnsUnauthorized()
    {
        ControllerTestContextFactory.SetUser(_controller);

        var result = await _controller.GetMyStats();

        Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
        var unauthorized = (UnauthorizedObjectResult)result.Result!;
        Assert.That(unauthorized.Value, Is.EqualTo("Invalid player ID"));
    }

    [Test]
    public async Task GetMyStats_WhenPlayerIdClaimIsInvalidGuid_ReturnsUnauthorized()
    {
        ControllerTestContextFactory.SetUser(_controller, rawPlayerId: "invalid-guid");

        var result = await _controller.GetMyStats();

        Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
        var unauthorized = (UnauthorizedObjectResult)result.Result!;
        Assert.That(unauthorized.Value, Is.EqualTo("Invalid player ID"));
    }

    [Test]
    public async Task GetMyStats_WhenStatsDontExist_ReturnsZeroStats()
    {
        var playerId = Guid.NewGuid();
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _statsServiceMock
            .Setup(s => s.GetStatsAsync(playerId))
            .ReturnsAsync((PlayerStatsSnapshot?)null);

        var result = await _controller.GetMyStats();

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value as PlayerStatsResponse;

        Assert.That(response, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(response!.TotalPoints, Is.Zero);
            Assert.That(response.Wins, Is.Zero);
            Assert.That(response.Losses, Is.Zero);
            Assert.That(response.WinRate, Is.EqualTo("0%"));
        });
    }

    [Test]
    public async Task GetMyStats_WhenStatsExist_ReturnsCalculatedWinRate()
    {
        var playerId = Guid.NewGuid();
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _statsServiceMock
            .Setup(s => s.GetStatsAsync(playerId))
            .ReturnsAsync(new PlayerStatsSnapshot
            {
                PlayerId = playerId,
                TotalPoints = 320,
                GamesWon = 3,
                GamesLost = 1
            });

        var result = await _controller.GetMyStats();

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value as PlayerStatsResponse;

        Assert.That(response, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(response!.TotalPoints, Is.EqualTo(320));
            Assert.That(response.Wins, Is.EqualTo(3));
            Assert.That(response.Losses, Is.EqualTo(1));
            Assert.That(response.WinRate, Is.EqualTo("75%"));
        });
    }

    // ==================== GetMyProfile ====================

    [Test]
    public async Task GetMyProfile_WhenPlayerIdClaimMissing_ReturnsUnauthorized()
    {
        ControllerTestContextFactory.SetUser(_controller);

        var result = await _controller.GetMyProfile();

        Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
        var unauthorized = (UnauthorizedObjectResult)result.Result!;
        Assert.That(unauthorized.Value, Is.EqualTo("Invalid player ID"));
    }

    [Test]
    public async Task GetMyProfile_WhenPlayerNotFound_ReturnsNotFound()
    {
        var playerId = Guid.NewGuid();
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _playerServiceMock.Setup(s => s.GetByIdAsync(playerId)).ReturnsAsync((PlayerEntity?)null);

        var result = await _controller.GetMyProfile();

        Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task GetMyProfile_WhenPlayerFound_ReturnsOkWithProfile()
    {
        var playerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _playerServiceMock.Setup(s => s.GetByIdAsync(playerId)).ReturnsAsync(new PlayerEntity
        {
            PlayerId = playerId,
            Username = "testuser",
            Email = "test@example.com",
            AvatarUrl = null,
            CreatedAt = now
        });

        var result = await _controller.GetMyProfile();

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value as PlayerProfileResponse;

        Assert.That(response, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(response!.PlayerId, Is.EqualTo(playerId));
            Assert.That(response.Username, Is.EqualTo("testuser"));
            Assert.That(response.Email, Is.EqualTo("test@example.com"));
        });
    }

    // ==================== UpdateMyProfile ====================

    [Test]
    public async Task UpdateMyProfile_WhenPlayerIdClaimMissing_ReturnsUnauthorized()
    {
        ControllerTestContextFactory.SetUser(_controller);

        var result = await _controller.UpdateMyProfile(new UpdatePlayerRequest("newname", null));

        Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
    }

    [Test]
    public async Task UpdateMyProfile_WhenUsernameAlreadyTaken_ReturnsConflict()
    {
        var playerId = Guid.NewGuid();
        var otherPlayerId = Guid.NewGuid();
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _playerServiceMock.Setup(s => s.GetByUsernameAsync("taken"))
            .ReturnsAsync(new PlayerEntity { PlayerId = otherPlayerId, Username = "taken" });

        var result = await _controller.UpdateMyProfile(new UpdatePlayerRequest("taken", null));

        Assert.That(result.Result, Is.TypeOf<ConflictObjectResult>());
        var conflict = (ConflictObjectResult)result.Result!;
        Assert.That(conflict.Value, Is.EqualTo("Username is already taken"));
    }

    [Test]
    public async Task UpdateMyProfile_WhenValid_ReturnsOkWithUpdatedPlayer()
    {
        var playerId = Guid.NewGuid();
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _playerServiceMock.Setup(s => s.GetByUsernameAsync("newname")).ReturnsAsync((PlayerEntity?)null);
        _playerServiceMock.Setup(s => s.UpdateAsync(playerId, "newname", null))
            .ReturnsAsync(new PlayerEntity
            {
                PlayerId = playerId,
                Username = "newname",
                Email = "test@example.com",
                CreatedAt = DateTimeOffset.UtcNow
            });

        var result = await _controller.UpdateMyProfile(new UpdatePlayerRequest("newname", null));

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var response = ok.Value as PlayerProfileResponse;

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Username, Is.EqualTo("newname"));
    }

    // ==================== DeleteMyAccount ====================

    [Test]
    public async Task DeleteMyAccount_WhenPlayerIdClaimMissing_ReturnsUnauthorized()
    {
        ControllerTestContextFactory.SetUser(_controller);

        var result = await _controller.DeleteMyAccount();

        Assert.That(result, Is.TypeOf<UnauthorizedObjectResult>());
    }

    [Test]
    public async Task DeleteMyAccount_WhenPlayerNotFound_ReturnsNotFound()
    {
        var playerId = Guid.NewGuid();
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _playerServiceMock.Setup(s => s.DeleteAsync(playerId)).ReturnsAsync(false);

        var result = await _controller.DeleteMyAccount();

        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task DeleteMyAccount_WhenValid_ReturnsNoContent()
    {
        var playerId = Guid.NewGuid();
        ControllerTestContextFactory.SetUser(_controller, playerId);

        _playerServiceMock.Setup(s => s.DeleteAsync(playerId)).ReturnsAsync(true);

        var result = await _controller.DeleteMyAccount();

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }
}
