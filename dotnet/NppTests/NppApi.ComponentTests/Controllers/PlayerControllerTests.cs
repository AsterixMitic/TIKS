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
}
