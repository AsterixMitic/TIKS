using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NppApi.Hubs;
using NppApi.Services;
using NppCore.Models;
using NppCore.Services.Persistence.Redis;
using NUnit.Framework;

namespace NppApi.ComponentTests.Services;

[TestFixture]
public class GameManagerServiceTests
{
    private Mock<IHubContext<GameHub>> _hubContextMock = null!;
    private Mock<IGameStateRepository> _gameStateRepoMock = null!;
    private Mock<ILogger<GameManagerService>> _loggerMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<IHubClients> _hubClientsMock = null!;
    private Mock<IClientProxy> _allClientsMock = null!;
    private Mock<ISingleClientProxy> _singleClientMock = null!;
    private GameManagerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _hubContextMock = new Mock<IHubContext<GameHub>>();
        _gameStateRepoMock = new Mock<IGameStateRepository>();
        _loggerMock = new Mock<ILogger<GameManagerService>>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();

        _hubClientsMock = new Mock<IHubClients>();
        _allClientsMock = new Mock<IClientProxy>();
        _singleClientMock = new Mock<ISingleClientProxy>();

        _hubClientsMock.Setup(c => c.All).Returns(_allClientsMock.Object);
        _hubClientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(_singleClientMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);

        _gameStateRepoMock.Setup(r => r.CreateReconnectTokenAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("test-token");

        _service = new GameManagerService(
            _hubContextMock.Object,
            _gameStateRepoMock.Object,
            _loggerMock.Object,
            _scopeFactoryMock.Object);
    }

    // ==================== CreateGameAsync ====================

    [Test]
    public async Task CreateGameAsync_ReturnsGameWithCorrectState()
    {
        var (game, token) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        Assert.Multiple(() =>
        {
            Assert.That(game, Is.Not.Null);
            Assert.That(game.State, Is.EqualTo(GameState.WaitingForPlayer));
            Assert.That(game.Player1, Is.Not.Null);
            Assert.That(game.Player1!.Name, Is.EqualTo("Player1"));
            Assert.That(game.Player1.ConnectionId, Is.EqualTo("conn-1"));
            Assert.That(game.Id, Has.Length.EqualTo(8));
        });
    }

    [Test]
    public async Task CreateGameAsync_InitializesBallAtCenter()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        Assert.Multiple(() =>
        {
            Assert.That(game.Ball.X, Is.EqualTo(Game.CanvasWidth / 2.0));
            Assert.That(game.Ball.Y, Is.EqualTo(Game.CanvasHeight / 2.0));
            Assert.That(game.Ball.VelocityX, Is.Not.EqualTo(0));
        });
    }

    [Test]
    public async Task CreateGameAsync_InitializesPaddlesAtCenter()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        var expectedY = (Game.CanvasHeight - Game.PaddleHeight) / 2.0;
        Assert.Multiple(() =>
        {
            Assert.That(game.Paddle1.Y, Is.EqualTo(expectedY));
            Assert.That(game.Paddle2.Y, Is.EqualTo(expectedY));
        });
    }

    [Test]
    public async Task CreateGameAsync_PersistsToRedis()
    {
        await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        _gameStateRepoMock.Verify(r => r.CreateGameAsync(It.IsAny<Game>()), Times.Once);
        _gameStateRepoMock.Verify(r => r.SetPlayerGameMappingAsync("conn-1", It.IsAny<string>()), Times.Once);
        _gameStateRepoMock.Verify(r => r.SetPlayerConnectedAsync(It.IsAny<string>(), 1, true, "conn-1"), Times.Once);
    }

    [Test]
    public async Task CreateGameAsync_ReturnsReconnectToken()
    {
        var (_, token) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        Assert.That(token, Is.EqualTo("test-token"));
        _gameStateRepoMock.Verify(r => r.CreateReconnectTokenAsync(
            It.IsAny<string>(), 1, "Player1", It.IsAny<TimeSpan>()), Times.Once);
    }

    [Test]
    public async Task CreateGameAsync_WhenRedisFails_ReturnsEmptyToken()
    {
        _gameStateRepoMock.Setup(r => r.CreateGameAsync(It.IsAny<Game>()))
            .ThrowsAsync(new Exception("Redis down"));

        var (game, token) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        Assert.Multiple(() =>
        {
            Assert.That(game, Is.Not.Null);
            Assert.That(token, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public async Task CreateGameAsync_SetsPlayerIdOnPlayer()
    {
        var playerId = Guid.NewGuid();

        var (game, _) = await _service.CreateGameAsync("conn-1", playerId, "Player1");

        Assert.That(game.Player1!.PlayerId, Is.EqualTo(playerId));
    }

    // ==================== JoinGameAsync ====================

    [Test]
    public async Task JoinGameAsync_WhenGameExists_ReturnsGameInPlayingState()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        var (joinedGame, token) = await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        Assert.Multiple(() =>
        {
            Assert.That(joinedGame, Is.Not.Null);
            Assert.That(joinedGame!.State, Is.EqualTo(GameState.Playing));
            Assert.That(joinedGame.Player2, Is.Not.Null);
            Assert.That(joinedGame.Player2!.Name, Is.EqualTo("Player2"));
        });
    }

    [Test]
    public async Task JoinGameAsync_WhenGameDoesNotExist_ReturnsNull()
    {
        var (game, token) = await _service.JoinGameAsync("nonexistent", "conn-2", Guid.NewGuid(), "Player2");

        Assert.Multiple(() =>
        {
            Assert.That(game, Is.Null);
            Assert.That(token, Is.Null);
        });
    }

    [Test]
    public async Task JoinGameAsync_WhenGameAlreadyPlaying_ReturnsNull()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        var (result, token) = await _service.JoinGameAsync(game.Id, "conn-3", Guid.NewGuid(), "Player3");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(token, Is.Null);
        });
    }

    [Test]
    public async Task JoinGameAsync_PersistsToRedis()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        _gameStateRepoMock.Verify(r => r.JoinGameAsync(game.Id, It.IsAny<Player>()), Times.Once);
        _gameStateRepoMock.Verify(r => r.SetPlayerGameMappingAsync("conn-2", game.Id), Times.Once);
        _gameStateRepoMock.Verify(r => r.SetPlayerConnectedAsync(game.Id, 2, true, "conn-2"), Times.Once);
    }

    [Test]
    public async Task JoinGameAsync_WhenRedisFails_ReturnsGameWithEmptyToken()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        _gameStateRepoMock.Setup(r => r.JoinGameAsync(It.IsAny<string>(), It.IsAny<Player>()))
            .ThrowsAsync(new Exception("Redis down"));

        var (joinedGame, token) = await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        Assert.Multiple(() =>
        {
            Assert.That(joinedGame, Is.Not.Null);
            Assert.That(token, Is.EqualTo(string.Empty));
        });
    }

    // ==================== GetOpenGames ====================

    [Test]
    public async Task GetOpenGames_ReturnsOnlyWaitingGames()
    {
        await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Host1");
        await _service.CreateGameAsync("conn-2", Guid.NewGuid(), "Host2");

        var openGames = _service.GetOpenGames();

        Assert.That(openGames, Has.Count.EqualTo(2));
        Assert.That(openGames.Select(g => g.HostName), Is.EquivalentTo(new[] { "Host1", "Host2" }));
    }

    [Test]
    public async Task GetOpenGames_ExcludesPlayingGames()
    {
        var (game1, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Host1");
        await _service.CreateGameAsync("conn-2", Guid.NewGuid(), "Host2");

        await _service.JoinGameAsync(game1.Id, "conn-3", Guid.NewGuid(), "Player2");

        var openGames = _service.GetOpenGames();

        Assert.That(openGames, Has.Count.EqualTo(1));
        Assert.That(openGames[0].HostName, Is.EqualTo("Host2"));
    }

    [Test]
    public void GetOpenGames_WhenNoGames_ReturnsEmptyList()
    {
        var openGames = _service.GetOpenGames();

        Assert.That(openGames, Is.Empty);
    }

    // ==================== GetGame ====================

    [Test]
    public async Task GetGame_WhenExists_ReturnsGame()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        var result = _service.GetGame(game.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(game.Id));
    }

    [Test]
    public void GetGame_WhenNotExists_ReturnsNull()
    {
        var result = _service.GetGame("nonexistent");

        Assert.That(result, Is.Null);
    }

    // ==================== GetGameId ====================

    [Test]
    public async Task GetGameId_WhenPlayerMapped_ReturnsGameId()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        var gameId = _service.GetGameId("conn-1");

        Assert.That(gameId, Is.EqualTo(game.Id));
    }

    [Test]
    public void GetGameId_WhenPlayerNotMapped_ReturnsNull()
    {
        var gameId = _service.GetGameId("unknown-conn");

        Assert.That(gameId, Is.Null);
    }

    // ==================== UpdatePaddle ====================

    [Test]
    public async Task UpdatePaddle_Player1_UpdatesPaddle1Y()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        _service.UpdatePaddle("conn-1", 150.0);

        Assert.That(game.Paddle1.Y, Is.EqualTo(150.0));
    }

    [Test]
    public async Task UpdatePaddle_Player2_UpdatesPaddle2Y()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        _service.UpdatePaddle("conn-2", 200.0);

        Assert.That(game.Paddle2.Y, Is.EqualTo(200.0));
    }

    [Test]
    public async Task UpdatePaddle_ClampsToCanvasBounds()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        _service.UpdatePaddle("conn-1", -50.0);
        Assert.That(game.Paddle1.Y, Is.EqualTo(0));

        _service.UpdatePaddle("conn-1", 9999.0);
        Assert.That(game.Paddle1.Y, Is.EqualTo(Game.CanvasHeight - Game.PaddleHeight));
    }

    [Test]
    public async Task UpdatePaddle_IgnoresNaN()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        var originalY = game.Paddle1.Y;

        _service.UpdatePaddle("conn-1", double.NaN);

        Assert.That(game.Paddle1.Y, Is.EqualTo(originalY));
    }

    [Test]
    public async Task UpdatePaddle_IgnoresInfinity()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        var originalY = game.Paddle1.Y;

        _service.UpdatePaddle("conn-1", double.PositiveInfinity);

        Assert.That(game.Paddle1.Y, Is.EqualTo(originalY));
    }

    [Test]
    public void UpdatePaddle_UnknownConnection_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _service.UpdatePaddle("unknown", 100.0));
    }

    // ==================== CancelGameAsync ====================

    [Test]
    public async Task CancelGameAsync_WhenHostCancels_ReturnsTrue()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        var result = await _service.CancelGameAsync("conn-1");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task CancelGameAsync_RemovesGameFromOpenGames()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        await _service.CancelGameAsync("conn-1");

        Assert.That(_service.GetGame(game.Id), Is.Null);
        Assert.That(_service.GetOpenGames(), Is.Empty);
    }

    [Test]
    public async Task CancelGameAsync_WhenConnectionNotFound_ReturnsFalse()
    {
        var result = await _service.CancelGameAsync("unknown-conn");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CancelGameAsync_WhenGameIsPlaying_ReturnsFalse()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        var result = await _service.CancelGameAsync("conn-1");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task CancelGameAsync_WhenNotHost_ReturnsFalse()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        // conn-2 is not the host
        var result = await _service.CancelGameAsync("conn-2");

        Assert.That(result, Is.False);
    }

    // ==================== BothPlayersConnected ====================

    [Test]
    public async Task BothPlayersConnected_WhenBothPresent_ReturnsTrue()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        var result = _service.BothPlayersConnected(game.Id);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task BothPlayersConnected_WhenOnlyOnePlayer_ReturnsFalse()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        var result = _service.BothPlayersConnected(game.Id);

        Assert.That(result, Is.False);
    }

    [Test]
    public void BothPlayersConnected_WhenGameNotFound_ReturnsFalse()
    {
        var result = _service.BothPlayersConnected("nonexistent");

        Assert.That(result, Is.False);
    }

    // ==================== HandlePlayerDisconnectAsync ====================

    [Test]
    public async Task HandlePlayerDisconnectAsync_WaitingGame_RemovesGame()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        await _service.HandlePlayerDisconnectAsync("conn-1");

        Assert.That(_service.GetGame(game.Id), Is.Null);
        Assert.That(_service.GetOpenGames(), Is.Empty);
    }

    [Test]
    public async Task HandlePlayerDisconnectAsync_PlayingGame_PausesGame()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        _gameStateRepoMock.Setup(r => r.GetPlayersConnectionStateAsync(game.Id))
            .ReturnsAsync(new PlayerConnectionState
            {
                Player1Connected = false,
                Player2Connected = true,
                Player2ConnectionId = "conn-2"
            });

        await _service.HandlePlayerDisconnectAsync("conn-1");

        Assert.That(game.State, Is.EqualTo(GameState.Paused));
        _gameStateRepoMock.Verify(r => r.MoveToPausedGamesAsync(game.Id), Times.Once);
    }

    [Test]
    public async Task HandlePlayerDisconnectAsync_UnknownConnection_DoesNothing()
    {
        await _service.HandlePlayerDisconnectAsync("unknown-conn");

        _gameStateRepoMock.Verify(r => r.RemoveGameAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task HandlePlayerDisconnectAsync_WaitingGame_BroadcastsLobbyUpdate()
    {
        await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        await _service.HandlePlayerDisconnectAsync("conn-1");

        _allClientsMock.Verify(c => c.SendCoreAsync(
            "LobbyUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ==================== ReconnectAsync ====================

    [Test]
    public async Task ReconnectAsync_InvalidToken_ReturnsFailure()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("bad-token"))
            .ReturnsAsync((ReconnectSession?)null);

        var result = await _service.ReconnectAsync("bad-token", "new-conn");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Invalid or expired token"));
        });
    }

    [Test]
    public async Task ReconnectAsync_ValidToken_Player1_UpdatesConnectionId()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");

        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("valid-token"))
            .ReturnsAsync(new ReconnectSession
            {
                Token = "valid-token",
                GameId = game.Id,
                PlayerNumber = 1,
                PlayerName = "Player1"
            });

        _gameStateRepoMock.Setup(r => r.GetPlayersConnectionStateAsync(game.Id))
            .ReturnsAsync(new PlayerConnectionState
            {
                Player1Connected = true,
                Player2Connected = true
            });

        var result = await _service.ReconnectAsync("valid-token", "new-conn-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.GameId, Is.EqualTo(game.Id));
            Assert.That(result.PlayerNumber, Is.EqualTo(1));
            Assert.That(result.PlayerName, Is.EqualTo("Player1"));
        });

        Assert.That(game.Player1!.ConnectionId, Is.EqualTo("new-conn-1"));
    }

    [Test]
    public async Task ReconnectAsync_BothPlayersReconnected_ResumesGame()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");
        await _service.JoinGameAsync(game.Id, "conn-2", Guid.NewGuid(), "Player2");
        game.State = GameState.Paused;

        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("token-p1"))
            .ReturnsAsync(new ReconnectSession
            {
                Token = "token-p1",
                GameId = game.Id,
                PlayerNumber = 1,
                PlayerName = "Player1"
            });

        _gameStateRepoMock.Setup(r => r.GetPlayersConnectionStateAsync(game.Id))
            .ReturnsAsync(new PlayerConnectionState
            {
                Player1Connected = true,
                Player2Connected = true
            });

        await _service.ReconnectAsync("token-p1", "new-conn-1");

        Assert.That(game.State, Is.EqualTo(GameState.Playing));
        _gameStateRepoMock.Verify(r => r.MoveFromPausedToPlayingAsync(game.Id), Times.Once);
    }

    [Test]
    public async Task ReconnectAsync_GameNoLongerExists_ReturnsFailure()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("token"))
            .ReturnsAsync(new ReconnectSession
            {
                Token = "token",
                GameId = "deleted-game",
                PlayerNumber = 1,
                PlayerName = "Player1"
            });

        _gameStateRepoMock.Setup(r => r.LoadAllGamesAsync())
            .ReturnsAsync(new List<Game>());

        var result = await _service.ReconnectAsync("token", "new-conn");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Game no longer exists"));
        });
    }

    [Test]
    public async Task ReconnectAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        var result = await _service.ReconnectAsync("token", "new-conn");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Reconnection failed"));
        });
    }

    // ==================== CheckPendingGameAsync ====================

    [Test]
    public async Task CheckPendingGameAsync_ValidToken_ReturnsSession()
    {
        var (game, _) = await _service.CreateGameAsync("conn-1", Guid.NewGuid(), "Player1");

        var session = new ReconnectSession
        {
            Token = "token",
            GameId = game.Id,
            PlayerNumber = 1,
            PlayerName = "Player1"
        };

        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("token"))
            .ReturnsAsync(session);

        var result = await _service.CheckPendingGameAsync("token");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.GameId, Is.EqualTo(game.Id));
    }

    [Test]
    public async Task CheckPendingGameAsync_InvalidToken_ReturnsNull()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("bad"))
            .ReturnsAsync((ReconnectSession?)null);

        var result = await _service.CheckPendingGameAsync("bad");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CheckPendingGameAsync_GameNoLongerInMemory_ChecksRedis()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("token"))
            .ReturnsAsync(new ReconnectSession
            {
                Token = "token",
                GameId = "removed-game",
                PlayerNumber = 1,
                PlayerName = "Player1"
            });

        _gameStateRepoMock.Setup(r => r.GetPlayersConnectionStateAsync("removed-game"))
            .ReturnsAsync(new PlayerConnectionState
            {
                Player1ConnectionId = null,
                Player2ConnectionId = null
            });

        var result = await _service.CheckPendingGameAsync("token");

        Assert.That(result, Is.Null);
        _gameStateRepoMock.Verify(r => r.RemoveReconnectTokenAsync("token"), Times.Once);
    }

    [Test]
    public async Task CheckPendingGameAsync_WhenExceptionThrown_ReturnsNull()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Redis error"));

        var result = await _service.CheckPendingGameAsync("token");

        Assert.That(result, Is.Null);
    }

    // ==================== StopAsync / Dispose ====================

    [Test]
    public async Task StopAsync_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(() => _service.StopAsync(CancellationToken.None));
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _service.Dispose());
    }
}
