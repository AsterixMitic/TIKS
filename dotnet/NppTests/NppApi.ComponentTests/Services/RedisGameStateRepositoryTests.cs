using Microsoft.Extensions.Logging;
using Moq;
using NppCore.Models;
using NppCore.Services.Persistence.Redis;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class RedisGameStateRepositoryTests
{
    private Mock<IRedisService> _redisMock = null!;
    private Mock<ILogger<RedisGameStateRepository>> _loggerMock = null!;
    private RedisGameStateRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _redisMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<RedisGameStateRepository>>();
        _repository = new RedisGameStateRepository(_redisMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task CreateGameAsync_StoresGameAndAddsToOpenGames()
    {
        var game = new Game
        {
            Id = "game-123",
            Player1 = new Player { ConnectionId = "conn-1", Name = "Player1", Score = 0 }
        };

        await _repository.CreateGameAsync(game);

        _redisMock.Verify(r => r.HashSetAsync(
            "game:game-123",
            It.Is<Dictionary<string, string>>(d => d["id"] == "game-123")), Times.Once);

        _redisMock.Verify(r => r.SetAddAsync("games:open", "game-123"), Times.Once);
    }

    [Test]
    public async Task JoinGameAsync_UpdatesPlayer2AndMovesToPlaying()
    {
        var player2 = new Player { ConnectionId = "conn-2", Name = "Player2", Score = 0 };

        var result = await _repository.JoinGameAsync("game-123", player2);

        Assert.That(result, Is.True);

        _redisMock.Verify(r => r.HashSetAsync(
            "game:game-123",
            It.Is<Dictionary<string, string>>(d =>
                d["player2_id"] == "conn-2" &&
                d["player2_name"] == "Player2" &&
                d["state"] == "Playing")), Times.Once);

        // MoveToPlayingGamesAsync removes from open, adds to playing
        _redisMock.Verify(r => r.SetRemoveAsync("games:open", "game-123"), Times.Once);
        _redisMock.Verify(r => r.SetAddAsync("games:playing", "game-123"), Times.Once);
    }

    [Test]
    public async Task RemoveGameAsync_DeletesGameAndCleansMappings()
    {
        await _repository.RemoveGameAsync("game-123");

        _redisMock.Verify(r => r.KeyDeleteAsync("game:game-123"), Times.Once);
        _redisMock.Verify(r => r.SetRemoveAsync("games:open", "game-123"), Times.Once);
        _redisMock.Verify(r => r.SetRemoveAsync("games:playing", "game-123"), Times.Once);
        _redisMock.Verify(r => r.SetRemoveAsync("games:paused", "game-123"), Times.Once);
    }

    [Test]
    public async Task CreateReconnectTokenAsync_StoresSessionWithExpiry()
    {
        var expiry = TimeSpan.FromMinutes(5);

        var token = await _repository.CreateReconnectTokenAsync("game-123", 1, "Player1", expiry);

        Assert.That(token, Is.Not.Null.And.Not.Empty);

        // Stores token hash
        _redisMock.Verify(r => r.HashSetAsync(
            It.Is<string>(s => s.StartsWith("reconnect:")),
            It.Is<Dictionary<string, string>>(d =>
                d["game_id"] == "game-123" &&
                d["player_number"] == "1" &&
                d["player_name"] == "Player1")), Times.Once);

        // Sets expiry on token key
        _redisMock.Verify(r => r.KeyExpireAsync(
            It.Is<string>(s => s.StartsWith("reconnect:")),
            expiry), Times.Once);

        // Adds token to game tokens set
        _redisMock.Verify(r => r.SetAddAsync("game:tokens:game-123", token), Times.Once);
    }

    [Test]
    public async Task GetReconnectSessionAsync_ValidToken_ReturnsSession()
    {
        var fields = new Dictionary<string, string>
        {
            ["game_id"] = "game-123",
            ["player_number"] = "1",
            ["player_name"] = "Player1",
            ["expires_at"] = DateTime.UtcNow.AddMinutes(5).ToString("O")
        };

        _redisMock.Setup(r => r.HashGetAllAsync("reconnect:my-token"))
            .ReturnsAsync(fields);

        var result = await _repository.GetReconnectSessionAsync("my-token");

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Token, Is.EqualTo("my-token"));
            Assert.That(result.GameId, Is.EqualTo("game-123"));
            Assert.That(result.PlayerNumber, Is.EqualTo(1));
            Assert.That(result.PlayerName, Is.EqualTo("Player1"));
        });
    }

    [Test]
    public async Task GetReconnectSessionAsync_InvalidToken_ReturnsNull()
    {
        _redisMock.Setup(r => r.HashGetAllAsync("reconnect:invalid"))
            .ReturnsAsync(new Dictionary<string, string>());

        var result = await _repository.GetReconnectSessionAsync("invalid");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task LoadAllGamesAsync_DeserializesAllGames()
    {
        _redisMock.Setup(r => r.KeysAsync("game:*"))
            .ReturnsAsync(new List<string> { "game:abc", "game:def" });

        var gameFields1 = new Dictionary<string, string>
        {
            ["id"] = "abc",
            ["state"] = "Playing",
            ["ball_x"] = "400", ["ball_y"] = "300",
            ["ball_vx"] = "5", ["ball_vy"] = "3",
            ["paddle1_y"] = "250", ["paddle2_y"] = "250",
            ["player1_id"] = "conn-1", ["player1_name"] = "P1", ["player1_score"] = "2",
            ["player2_id"] = "conn-2", ["player2_name"] = "P2", ["player2_score"] = "1"
        };

        var gameFields2 = new Dictionary<string, string>
        {
            ["id"] = "def",
            ["state"] = "WaitingForPlayer",
            ["ball_x"] = "400", ["ball_y"] = "300",
            ["ball_vx"] = "0", ["ball_vy"] = "0",
            ["paddle1_y"] = "250", ["paddle2_y"] = "250",
            ["player1_id"] = "conn-3", ["player1_name"] = "P3", ["player1_score"] = "0"
        };

        _redisMock.Setup(r => r.HashGetAllAsync("game:abc")).ReturnsAsync(gameFields1);
        _redisMock.Setup(r => r.HashGetAllAsync("game:def")).ReturnsAsync(gameFields2);

        var games = await _repository.LoadAllGamesAsync();

        Assert.That(games, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(games[0].Id, Is.EqualTo("abc"));
            Assert.That(games[0].State, Is.EqualTo(GameState.Playing));
            Assert.That(games[0].Player1!.Score, Is.EqualTo(2));
            Assert.That(games[1].Id, Is.EqualTo("def"));
            Assert.That(games[1].State, Is.EqualTo(GameState.WaitingForPlayer));
        });
    }
}
