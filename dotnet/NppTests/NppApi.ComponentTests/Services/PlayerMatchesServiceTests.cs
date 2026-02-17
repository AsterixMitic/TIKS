using Moq;
using NppCore.Models;
using NppCore.Services.Features.Match;
using NppCore.Services.Persistence.Cassandra;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class PlayerMatchesServiceTests
{
    private Mock<ICassandraService> _cassandraMock = null!;
    private PlayerMatchesService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cassandraMock = new Mock<ICassandraService>();
        _service = new PlayerMatchesService(_cassandraMock.Object);
    }

    [Test]
    public async Task CreateAsync_ReturnsCreatedMatch()
    {
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var matchTime = DateTimeOffset.UtcNow;

        var result = await _service.CreateAsync(
            playerId, opponentId, "2026", matchTime, matchId, "opponent", "5:3", "WIN");

        Assert.Multiple(() =>
        {
            Assert.That(result.PlayerId, Is.EqualTo(playerId));
            Assert.That(result.OpponentId, Is.EqualTo(opponentId));
            Assert.That(result.Year, Is.EqualTo("2026"));
            Assert.That(result.Score, Is.EqualTo("5:3"));
            Assert.That(result.Result, Is.EqualTo("WIN"));
        });

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("player_matches")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Test]
    public async Task GetByYearAsync_ReturnsPaginatedResults()
    {
        var playerId = Guid.NewGuid();
        var matches = Enumerable.Range(1, 10).Select(i => new PlayerMatches
        {
            PlayerId = playerId, Year = "2026", Score = $"{i}:0", Result = "WIN",
            Match_time = DateTimeOffset.UtcNow.AddHours(-i), MatchId = Guid.NewGuid()
        }).ToList();

        _cassandraMock.Setup(c => c.QueryAsync<PlayerMatches>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(matches);

        // Page 2, limit 3 → items 4,5,6
        var result = (await _service.GetByYearAsync("2026", playerId, page: 2, limit: 3)).ToList();

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetByYearAsync_EmptyResult_ReturnsEmpty()
    {
        _cassandraMock.Setup(c => c.QueryAsync<PlayerMatches>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(new List<PlayerMatches>());

        var result = await _service.GetByYearAsync("2026", Guid.NewGuid(), 1, 5);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task CreateHistoryAsync_ExtractsCorrectPeriodBucket()
    {
        var matchTime = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);
        var matchId = Guid.NewGuid();

        var result = await _service.CreateHistoryAsync(
            matchTime, "player1", "player2", "5:3", "WIN", matchId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Period, Is.EqualTo("2026-03"));
            Assert.That(result.player1Username, Is.EqualTo("player1"));
            Assert.That(result.player2Username, Is.EqualTo("player2"));
        });

        _cassandraMock.Verify(c => c.ExecuteAsync(
            It.Is<string>(s => s.Contains("recent_matches")),
            It.IsAny<object[]>()), Times.Once);
    }

    [Test]
    public async Task GetHistoryAsync_ReturnsPaginatedResults()
    {
        var matches = Enumerable.Range(1, 8).Select(i => new MatchHistory
        {
            Period = "2026-02", Match_time = DateTimeOffset.UtcNow.AddHours(-i),
            MatchId = Guid.NewGuid(), player1Username = "p1", player2Username = "p2",
            Score = "5:3", Result = "WIN"
        }).ToList();

        _cassandraMock.Setup(c => c.QueryAsync<MatchHistory>(
            It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(matches);

        // Page 1, limit 5 → first 5 items
        var result = (await _service.GetHistoryAsync("2026-02", page: 1, limit: 5)).ToList();

        Assert.That(result, Has.Count.EqualTo(5));
    }
}
