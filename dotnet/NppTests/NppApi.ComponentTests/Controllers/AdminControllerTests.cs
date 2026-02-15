using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NppApi.Controllers;
using NppCore.Models;
using NppCore.Services.Features.Leaderboard;
using NppCore.Services.Persistence.Cassandra;
using NUnit.Framework;

namespace NppApi.ComponentTests.Controllers;

[TestFixture]
public class AdminControllerTests
{
    private Mock<ICassandraService> _cassandraMock = null!;
    private Mock<ILeaderboardService> _leaderboardServiceMock = null!;
    private Mock<ILogger<AdminController>> _loggerMock = null!;
    private AdminController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _cassandraMock = new Mock<ICassandraService>();
        _leaderboardServiceMock = new Mock<ILeaderboardService>();
        _loggerMock = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(_cassandraMock.Object, _leaderboardServiceMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task AddPoints_WhenRequestIsValid_ReturnsOk()
    {
        _cassandraMock
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(Task.CompletedTask);

        var request = new AddPointsRequest(Guid.NewGuid(), 50, 1, 0);
        var result = await _controller.AddPoints(request);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        _cassandraMock.Verify(
            c => c.ExecuteAsync(It.Is<string>(q => q.Contains("UPDATE player_stats")), It.IsAny<object[]>()),
            Times.Once);
    }

    [Test]
    public async Task AddPoints_WhenCassandraFails_ReturnsStatus500()
    {
        _cassandraMock
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<object[]>()))
            .ThrowsAsync(new Exception("cassandra-down"));

        var request = new AddPointsRequest(Guid.NewGuid(), 50, 1, 0);
        var result = await _controller.AddPoints(request);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)result;
        Assert.That(objectResult.StatusCode, Is.EqualTo(500));
    }

    [Test]
    public async Task SnapshotLeaderboards_WhenDataExists_UpdatesLeaderboardsAndReturnsOk()
    {
        var playerId = Guid.NewGuid();

        _cassandraMock
            .Setup(c => c.QueryAsync<PlayerStatsSnapshot>(It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new List<PlayerStatsSnapshot>
            {
                new()
                {
                    PlayerId = playerId,
                    TotalPoints = 100,
                    GamesWon = 2,
                    GamesLost = 1
                }
            });

        _cassandraMock
            .Setup(c => c.QueryFirstOrDefaultAsync<PlayerInfo>(It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerInfo
            {
                PlayerId = playerId,
                Username = "player_one"
            });

        _leaderboardServiceMock
            .Setup(s => s.AddOrUpdateWinsLeaderboardAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _leaderboardServiceMock
            .Setup(s => s.AddOrUpdateGlobalLeaderboardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SnapshotLeaderboards();

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        _leaderboardServiceMock.Verify(
            s => s.AddOrUpdateWinsLeaderboardAsync(It.IsAny<string>(), playerId, "player_one", 2),
            Times.Once);
        _leaderboardServiceMock.Verify(
            s => s.AddOrUpdateGlobalLeaderboardAsync(It.IsAny<string>(), It.IsAny<string>(), playerId, "player_one", 100),
            Times.Exactly(3));
    }

    [Test]
    public async Task SnapshotLeaderboards_WhenQueryFails_ReturnsStatus500()
    {
        _cassandraMock
            .Setup(c => c.QueryAsync<PlayerStatsSnapshot>(It.IsAny<string>(), It.IsAny<object[]>()))
            .ThrowsAsync(new Exception("query-failed"));

        var result = await _controller.SnapshotLeaderboards();

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)result;
        Assert.That(objectResult.StatusCode, Is.EqualTo(500));
    }
}
