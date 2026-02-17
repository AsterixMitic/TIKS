using Moq;
using NppCore.Constants;
using NppCore.Models;
using NppCore.Services.Features.Leaderboard;
using NppCore.Services.Persistence.Cassandra;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class LeaderboardServiceTests
{
    private Mock<ICassandraService> _cassandraMock = null!;
    private LeaderboardService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cassandraMock = new Mock<ICassandraService>();
        _service = new LeaderboardService(_cassandraMock.Object);
    }

    [Test]
    public async Task GetGlobalLeaderboard_ReturnsEntriesWithCorrectRanks()
    {
        var playerId1 = Guid.NewGuid();
        var playerId2 = Guid.NewGuid();
        var entries = new List<GlobalLeaderboardEntry>
        {
            new() { PeriodType = "MONTHLY", PeriodId = "2026-01", RankScore = 500, PlayerId = playerId1, Username = "player1" },
            new() { PeriodType = "MONTHLY", PeriodId = "2026-01", RankScore = 300, PlayerId = playerId2, Username = "player2" }
        };

        _cassandraMock.Setup(c => c.QueryAsync<GlobalLeaderboardEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(entries);

        var result = await _service.GetGlobalLeaderboardAsync("MONTHLY", "2026-01", 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.PeriodType, Is.EqualTo("MONTHLY"));
            Assert.That(result.PeriodId, Is.EqualTo("2026-01"));
            Assert.That(result.Entries, Has.Count.EqualTo(2));
            Assert.That(result.Entries[0].Rank, Is.EqualTo(1));
            Assert.That(result.Entries[1].Rank, Is.EqualTo(2));
            Assert.That(result.Entries[0].Score, Is.EqualTo(500));
        });
    }

    [Test]
    public async Task GetGlobalLeaderboard_EmptyResult_ReturnsEmptyEntries()
    {
        _cassandraMock.Setup(c => c.QueryAsync<GlobalLeaderboardEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new List<GlobalLeaderboardEntry>());

        var result = await _service.GetGlobalLeaderboardAsync("MONTHLY", "2026-01");

        Assert.That(result.Entries, Is.Empty);
    }

    [Test]
    public async Task AddOrUpdateGlobal_NewEntry_InsertsEntry()
    {
        var playerId = Guid.NewGuid();

        // No existing entry
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<GlobalLeaderboardByPlayerEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((GlobalLeaderboardByPlayerEntry?)null);

        await _service.AddOrUpdateGlobalLeaderboardAsync("MONTHLY", "2026-01", playerId, "player1", 500);

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("INSERT") && s.Contains("BATCH")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Test]
    public async Task AddOrUpdateGlobal_ExistingChanged_DeletesAndReinserts()
    {
        var playerId = Guid.NewGuid();
        var existing = new GlobalLeaderboardByPlayerEntry
        {
            PeriodType = "MONTHLY", PeriodId = "2026-01",
            PlayerId = playerId, Username = "player1", RankScore = 300
        };

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<GlobalLeaderboardByPlayerEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(existing);

        await _service.AddOrUpdateGlobalLeaderboardAsync("MONTHLY", "2026-01", playerId, "player1", 500);

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("DELETE") && s.Contains("BATCH")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Test]
    public async Task GetWinsLeaderboard_ReturnsCategoryAndEntries()
    {
        var entries = new List<WinsLeaderboardEntry>
        {
            new() { Category = "most_wins", GamesWon = 10, PlayerId = Guid.NewGuid(), Username = "winner" }
        };

        _cassandraMock.Setup(c => c.QueryAsync<WinsLeaderboardEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(entries);

        var result = await _service.GetWinsLeaderboardAsync("most_wins", 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.Category, Is.EqualTo("most_wins"));
            Assert.That(result.Entries, Has.Count.EqualTo(1));
            Assert.That(result.Entries[0].Score, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task GetPlayerStreak_ExistingPlayer_ReturnsStreakDto()
    {
        var playerId = Guid.NewGuid();
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStreak>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerStreak { PlayerId = playerId, CurrentStreak = 3, LongestStreak = 5, LastResult = "WIN" });

        var result = await _service.GetPlayerStreakAsync(playerId);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.CurrentStreak, Is.EqualTo(3));
            Assert.That(result.LongestStreak, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetPlayerStreak_NonExistent_ReturnsNull()
    {
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStreak>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((PlayerStreak?)null);

        var result = await _service.GetPlayerStreakAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UpdatePlayerStreak_FirstWin_SetsStreakTo1()
    {
        var playerId = Guid.NewGuid();

        // No existing streak
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStreak>(
            It.Is<string>(s => s.Contains("player_current_streak")),
            It.IsAny<object[]>()))
            .ReturnsAsync((PlayerStreak?)null);

        // No existing streak leaderboard entry
        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<StreakLeaderboardByPlayerEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((StreakLeaderboardByPlayerEntry?)null);

        await _service.UpdatePlayerStreakAsync(playerId, "player1", won: true);

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("player_current_streak")),
            It.Is<object[]>(args =>
                (int)args[1] == 1 && // currentStreak
                (int)args[2] == 1    // longestStreak
            )), Times.Once);
    }

    [Test]
    public async Task UpdatePlayerStreak_ConsecutiveWins_IncrementsStreak()
    {
        var playerId = Guid.NewGuid();

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStreak>(
            It.Is<string>(s => s.Contains("player_current_streak")),
            It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerStreak { PlayerId = playerId, CurrentStreak = 2, LongestStreak = 2, LastResult = "WIN" });

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<StreakLeaderboardByPlayerEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync((StreakLeaderboardByPlayerEntry?)null);

        await _service.UpdatePlayerStreakAsync(playerId, "player1", won: true);

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("player_current_streak")),
            It.Is<object[]>(args =>
                (int)args[1] == 3 && // currentStreak
                (int)args[2] == 3    // longestStreak
            )), Times.Once);
    }

    [Test]
    public async Task UpdatePlayerStreak_LossAfterWins_ResetsCurrentKeepsLongest()
    {
        var playerId = Guid.NewGuid();

        _cassandraMock.Setup(c => c.QueryFirstOrDefaultAsync<PlayerStreak>(
            It.Is<string>(s => s.Contains("player_current_streak")),
            It.IsAny<object[]>()))
            .ReturnsAsync(new PlayerStreak { PlayerId = playerId, CurrentStreak = 3, LongestStreak = 5, LastResult = "WIN" });

        await _service.UpdatePlayerStreakAsync(playerId, "player1", won: false);

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("player_current_streak")),
            It.Is<object[]>(args =>
                (int)args[1] == 0 && // currentStreak reset
                (int)args[2] == 5    // longestStreak unchanged
            )), Times.Once);
    }

    [Test]
    public async Task GetStreakLeaderboard_ReturnsEntries()
    {
        var entries = new List<StreakLeaderboardEntry>
        {
            new() { Category = "global_all_time", LongestStreak = 10, PlayerId = Guid.NewGuid(), Username = "streaker" }
        };

        _cassandraMock.Setup(c => c.QueryAsync<StreakLeaderboardEntry>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(entries);

        var result = await _service.GetStreakLeaderboardAsync("global_all_time", 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.Category, Is.EqualTo("global_all_time"));
            Assert.That(result.Entries, Has.Count.EqualTo(1));
            Assert.That(result.Entries[0].Score, Is.EqualTo(10));
        });
    }
}
