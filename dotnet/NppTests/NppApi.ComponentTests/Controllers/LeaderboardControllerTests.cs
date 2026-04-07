using Microsoft.AspNetCore.Mvc;
using Moq;
using NppApi.Controllers;
using NppCore.Models;
using NppCore.Services.Features.Leaderboard;
using NUnit.Framework;

namespace NppApi.ComponentTests.Controllers;

[TestFixture]
public class LeaderboardControllerTests
{
    private Mock<ILeaderboardService> _leaderboardServiceMock = null!;
    private LeaderboardController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _leaderboardServiceMock = new Mock<ILeaderboardService>();
        _controller = new LeaderboardController(_leaderboardServiceMock.Object);
    }

    [Test]
    public async Task GetGlobalLeaderboard_WhenPeriodTypeMissing_ReturnsBadRequest()
    {
        var result = await _controller.GetGlobalLeaderboard("", "2026-02", 10);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result.Result!;
        Assert.That(badRequest.Value, Is.EqualTo("periodType and periodId are required"));
    }

    [Test]
    public async Task GetGlobalLeaderboard_WhenPeriodIdMissing_ReturnsBadRequest()
    {
        var result = await _controller.GetGlobalLeaderboard("MONTHLY", "", 10);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result.Result!;
        Assert.That(badRequest.Value, Is.EqualTo("periodType and periodId are required"));
    }

    [Test]
    public async Task GetGlobalLeaderboard_WhenRequestValid_ReturnsOk()
    {
        var response = new GlobalLeaderboardResponse(
            "MONTHLY",
            "2026-02",
            new List<LeaderboardEntryDto>
            {
                new(1, Guid.NewGuid(), "player_1", 120)
            });

        _leaderboardServiceMock
            .Setup(s => s.GetGlobalLeaderboardAsync("MONTHLY", "2026-02", 10))
            .ReturnsAsync(response);

        var result = await _controller.GetGlobalLeaderboard("MONTHLY", "2026-02", 10);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        Assert.That(ok.Value, Is.EqualTo(response));
    }

    [Test]
    public async Task GetPlayerStreak_WhenPlayerNotFound_ReturnsNotFound()
    {
        var playerId = Guid.NewGuid();

        _leaderboardServiceMock
            .Setup(s => s.GetPlayerStreakAsync(playerId))
            .ReturnsAsync((PlayerStreakDto?)null);

        var result = await _controller.GetPlayerStreak(playerId);

        Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task GetPlayerStreak_WhenFound_ReturnsOk()
    {
        var playerId = Guid.NewGuid();
        var streak = new PlayerStreakDto(playerId, 4, 9, "WIN");

        _leaderboardServiceMock
            .Setup(s => s.GetPlayerStreakAsync(playerId))
            .ReturnsAsync(streak);

        var result = await _controller.GetPlayerStreak(playerId);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        Assert.That(ok.Value, Is.EqualTo(streak));
    }

    [Test]
    public async Task GetWinsLeaderboard_WhenCalled_ReturnsOk()
    {
        var response = new WinsLeaderboardResponse("most_wins", new List<LeaderboardEntryDto>());

        _leaderboardServiceMock
            .Setup(s => s.GetWinsLeaderboardAsync("most_wins", 20))
            .ReturnsAsync(response);

        var result = await _controller.GetWinsLeaderboard("most_wins", 20);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        Assert.That(ok.Value, Is.EqualTo(response));
    }

    [Test]
    public async Task GetStreakLeaderboard_WhenCalled_ReturnsOk()
    {
        var response = new StreakLeaderboardResponse("global_all_time", new List<LeaderboardEntryDto>());

        _leaderboardServiceMock
            .Setup(s => s.GetStreakLeaderboardAsync("global_all_time", 20))
            .ReturnsAsync(response);

        var result = await _controller.GetStreakLeaderboard("global_all_time", 20);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        Assert.That(ok.Value, Is.EqualTo(response));
    }

    [Test]
    public async Task GetPlayerStreak_ServiceCalledWithCorrectPlayerId()
    {
        var playerId = Guid.NewGuid();
        var streak = new PlayerStreakDto(playerId, 2, 7, "WIN");

        _leaderboardServiceMock
            .Setup(s => s.GetPlayerStreakAsync(playerId))
            .ReturnsAsync(streak);

        await _controller.GetPlayerStreak(playerId);

        _leaderboardServiceMock.Verify(s => s.GetPlayerStreakAsync(playerId), Times.Once);
    }

    [Test]
    public async Task GetWinsLeaderboard_WhenServiceReturnsEmpty_ReturnsOkWithEmptyList()
    {
        var response = new WinsLeaderboardResponse("most_wins", new List<LeaderboardEntryDto>());

        _leaderboardServiceMock
            .Setup(s => s.GetWinsLeaderboardAsync("most_wins", 10))
            .ReturnsAsync(response);

        var result = await _controller.GetWinsLeaderboard("most_wins", 10);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var body = ok.Value as WinsLeaderboardResponse;
        Assert.That(body!.Entries, Is.Empty);
    }

    [Test]
    public async Task GetWinsLeaderboard_WhenLimitNotProvided_UsesDefaultLimit()
    {
        var response = new WinsLeaderboardResponse("most_wins", new List<LeaderboardEntryDto>());

        _leaderboardServiceMock
            .Setup(s => s.GetWinsLeaderboardAsync("most_wins", 10))
            .ReturnsAsync(response);

        var result = await _controller.GetWinsLeaderboard("most_wins", 10);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        _leaderboardServiceMock.Verify(s => s.GetWinsLeaderboardAsync("most_wins", 10), Times.Once);
    }

    [Test]
    public async Task GetStreakLeaderboard_WhenServiceReturnsEmpty_ReturnsOkWithEmptyList()
    {
        var response = new StreakLeaderboardResponse("global_all_time", new List<LeaderboardEntryDto>());

        _leaderboardServiceMock
            .Setup(s => s.GetStreakLeaderboardAsync("global_all_time", 10))
            .ReturnsAsync(response);

        var result = await _controller.GetStreakLeaderboard("global_all_time", 10);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = (OkObjectResult)result.Result!;
        var body = ok.Value as StreakLeaderboardResponse;
        Assert.That(body!.Entries, Is.Empty);
    }

    [Test]
    public async Task GetStreakLeaderboard_WhenLimitNotProvided_UsesDefaultLimit()
    {
        var response = new StreakLeaderboardResponse("global_all_time", new List<LeaderboardEntryDto>());

        _leaderboardServiceMock
            .Setup(s => s.GetStreakLeaderboardAsync("global_all_time", 10))
            .ReturnsAsync(response);

        var result = await _controller.GetStreakLeaderboard("global_all_time", 10);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        _leaderboardServiceMock.Verify(s => s.GetStreakLeaderboardAsync("global_all_time", 10), Times.Once);
    }
}
