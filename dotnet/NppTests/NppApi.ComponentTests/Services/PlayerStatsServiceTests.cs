using Microsoft.Extensions.Logging;
using Moq;
using NppCore.Constants;
using NppCore.Models;
using NppCore.Services.Features.Leaderboard;
using NppCore.Services.Features.Player;
using NppCore.Services.Persistence.Cassandra;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class PlayerStatsServiceTests
{
    private Mock<ICassandraService> _cassandraMock = null!;
    private Mock<ILeaderboardService> _leaderboardMock = null!;
    private Mock<ILogger<PlayerStatsService>> _loggerMock = null!;
    private PlayerStatsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cassandraMock = new Mock<ICassandraService>();
        _leaderboardMock = new Mock<ILeaderboardService>();
        _loggerMock = new Mock<ILogger<PlayerStatsService>>();
        _service = new PlayerStatsService(_cassandraMock.Object, _leaderboardMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task GetStatsAsync_ExistingPlayer_ReturnsStats()
    {
        var playerId = Guid.NewGuid();
        var stats = new PlayerStatsSnapshot { PlayerId = playerId, TotalPoints = 100, GamesWon = 5, GamesLost = 3 };

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStatsSnapshot>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(stats);

        var result = await _service.GetStatsAsync(playerId);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalPoints, Is.EqualTo(100));
            Assert.That(result.GamesWon, Is.EqualTo(5));
            Assert.That(result.GamesLost, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetStatsAsync_NonExistent_ReturnsNull()
    {
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStatsSnapshot>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((PlayerStatsSnapshot?)null);

        var result = await _service.GetStatsAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UpdateStatsAfterMatch_UpdatesWinnerAndLoserCounters()
    {
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();
        var matchTime = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);

        // Return stats for leaderboard updates (block 4)
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStatsSnapshot>(
            It.Is<string>(s => s.Contains("player_stats")),
            It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerStatsSnapshot { TotalPoints = 50, GamesWon = 1, GamesLost = 0 });

        await _service.UpdateStatsAfterMatchAsync(
            winnerId, "winner", 5, loserId, "loser", 3, Guid.NewGuid(), matchTime);

        // Winner stats updated
        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("games_won = games_won + 1")),
            It.IsAny<object[]>()), Times.Once);

        // Loser stats updated
        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("games_lost = games_lost + 1")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Test]
    public async Task UpdateStatsAfterMatch_CallsUpdateStreakForBothPlayers()
    {
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();
        var matchTime = DateTimeOffset.UtcNow;

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStatsSnapshot>(
            It.Is<string>(s => s.Contains("player_stats")),
            It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerStatsSnapshot { TotalPoints = 50, GamesWon = 1, GamesLost = 0 });

        await _service.UpdateStatsAfterMatchAsync(
            winnerId, "winner", 5, loserId, "loser", 3, Guid.NewGuid(), matchTime);

        _leaderboardMock.Verify(l => l.UpdatePlayerStreakAsync(winnerId, "winner", true), Times.Once);
        _leaderboardMock.Verify(l => l.UpdatePlayerStreakAsync(loserId, "loser", false), Times.Once);
    }

    [Test]
    public async Task UpdateStatsAfterMatch_CalculatesPointsCorrectly()
    {
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();
        var matchTime = DateTimeOffset.UtcNow;
        int winnerScore = 5;
        long expectedWinnerPoints = winnerScore * GameConstants.PointsPerScore;

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStatsSnapshot>(
            It.Is<string>(s => s.Contains("player_stats")),
            It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerStatsSnapshot { TotalPoints = 50, GamesWon = 1, GamesLost = 0 });

        await _service.UpdateStatsAfterMatchAsync(
            winnerId, "winner", winnerScore, loserId, "loser", 3, Guid.NewGuid(), matchTime);

        // Verify winner points calculation: score * PointsPerScore
        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("total_points = total_points + ?") && s.Contains("games_won")),
            It.Is<object[]>(args => (long)args[0] == expectedWinnerPoints)),
            Times.Once);
    }
}
