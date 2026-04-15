using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using NppApi.Hubs;
using NppApi.Services;
using NppCore.Models;
using NppCore.Services.Persistence.Redis;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace NppApi.ComponentTests.Hubs;

[TestFixture]
public class GameHubTests
{
    private Mock<IHubContext<GameHub>> _hubContextMock = null!;
    private Mock<IGameStateRepository> _gameStateRepoMock = null!;
    private Mock<ILogger<GameManagerService>> _managerLoggerMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<ILogger<GameHub>> _hubLoggerMock = null!;
    private GameManagerService _gameManager = null!;
    private GameHub _hub = null!;

    private Mock<IHubCallerClients> _callerClientsMock = null!;
    private Mock<ISingleClientProxy> _callerMock = null!;
    private Mock<IClientProxy> _allMock = null!;
    private Mock<IGroupManager> _groupsMock = null!;
    private Mock<HubCallerContext> _contextMock = null!;

    private readonly Guid _testPlayerId = Guid.NewGuid();
    private const string TestConnectionId = "test-conn-id";

    [SetUp]
    public void SetUp()
    {
        _hubContextMock = new Mock<IHubContext<GameHub>>();
        _gameStateRepoMock = new Mock<IGameStateRepository>();
        _managerLoggerMock = new Mock<ILogger<GameManagerService>>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _hubLoggerMock = new Mock<ILogger<GameHub>>();
        
        var hubClients = new Mock<IHubClients>();
        var hubAllClient = new Mock<IClientProxy>();
        var hubSingleClient = new Mock<ISingleClientProxy>();
        hubClients.Setup(c => c.All).Returns(hubAllClient.Object);
        hubClients.Setup(c => c.Client(It.IsAny<string>())).Returns(hubSingleClient.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(hubClients.Object);

        _gameStateRepoMock.Setup(r => r.CreateReconnectTokenAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("reconnect-token-123");

        _gameManager = new GameManagerService(
            _hubContextMock.Object,
            _gameStateRepoMock.Object,
            _managerLoggerMock.Object,
            _scopeFactoryMock.Object);

        _callerClientsMock = new Mock<IHubCallerClients>();
        _callerMock = new Mock<ISingleClientProxy>();
        _allMock = new Mock<IClientProxy>();
        _groupsMock = new Mock<IGroupManager>();
        _contextMock = new Mock<HubCallerContext>();

        _callerClientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
        _callerClientsMock.Setup(c => c.All).Returns(_allMock.Object);
        _callerClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(new Mock<IClientProxy>().Object);
        _contextMock.Setup(c => c.ConnectionId).Returns(TestConnectionId);

        _hub = new GameHub(_gameManager, _hubLoggerMock.Object);
        _hub.Clients = _callerClientsMock.Object;
        _hub.Groups = _groupsMock.Object;
        _hub.Context = _contextMock.Object;

        SetupAuthenticatedUser(_testPlayerId);
    }

    private void SetupAuthenticatedUser(Guid playerId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, playerId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _contextMock.Setup(c => c.User).Returns(principal);
    }

    private void SetupUnauthenticatedUser()
    {
        _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal());
    }

    // ==================== CreateGame ====================

    [Test]
    public async Task CreateGame_AuthenticatedUser_CreatesGameAndJoinsGroup()
    {
        await _hub.CreateGame("Player1");

        _groupsMock.Verify(g => g.AddToGroupAsync(
            TestConnectionId,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGame_SendsReconnectTokenToCaller()
    {
        await _hub.CreateGame("Player1");

        _callerMock.Verify(c => c.SendCoreAsync(
            "ReconnectToken",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGame_BroadcastsLobbyUpdate()
    {
        await _hub.CreateGame("Player1");

        _allMock.Verify(c => c.SendCoreAsync(
            "LobbyUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void CreateGame_UnauthenticatedUser_ThrowsHubException()
    {
        SetupUnauthenticatedUser();

        Assert.ThrowsAsync<HubException>(() => _hub.CreateGame("Player1"));
    }

    // ==================== JoinGame ====================

    [Test]
    public async Task JoinGame_ValidGame_JoinsGroupAndBroadcasts()
    {
        // Create a game first
        await _hub.CreateGame("Host");
        var openGames = _gameManager.GetOpenGames();
        var gameId = openGames[0].GameId;

        // Switch to a different connection for player 2
        var player2ConnId = "conn-player2";
        _contextMock.Setup(c => c.ConnectionId).Returns(player2ConnId);
        SetupAuthenticatedUser(Guid.NewGuid());

        await _hub.JoinGame(gameId, "Player2");

        _groupsMock.Verify(g => g.AddToGroupAsync(
            player2ConnId,
            gameId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task JoinGame_ValidGame_BroadcastsLobbyUpdateAndGameStarted()
    {
        await _hub.CreateGame("Host");
        var gameId = _gameManager.GetOpenGames()[0].GameId;

        _contextMock.Setup(c => c.ConnectionId).Returns("conn-player2");
        SetupAuthenticatedUser(Guid.NewGuid());

        var groupClientMock = new Mock<IClientProxy>();
        _callerClientsMock.Setup(c => c.Group(gameId)).Returns(groupClientMock.Object);

        await _hub.JoinGame(gameId, "Player2");

        // LobbyUpdated broadcast (2 times: once for create, once for join)
        _allMock.Verify(c => c.SendCoreAsync(
            "LobbyUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        // GameStarted to the group
        groupClientMock.Verify(c => c.SendCoreAsync(
            "GameStarted",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task JoinGame_NonexistentGame_SendsJoinFailed()
    {
        await _hub.JoinGame("nonexistent-id", "Player2");

        _callerMock.Verify(c => c.SendCoreAsync(
            "JoinFailed",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void JoinGame_UnauthenticatedUser_ThrowsHubException()
    {
        SetupUnauthenticatedUser();

        Assert.ThrowsAsync<HubException>(() => _hub.JoinGame("game-id", "Player2"));
    }

    // ==================== GetLobby ====================

    [Test]
    public async Task GetLobby_SendsLobbyUpdatedToCaller()
    {
        await _hub.GetLobby();

        _callerMock.Verify(c => c.SendCoreAsync(
            "LobbyUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetLobby_AfterGameCreated_IncludesOpenGame()
    {
        await _hub.CreateGame("Host1");

        var openGames = _gameManager.GetOpenGames();

        Assert.That(openGames, Has.Count.EqualTo(1));
        Assert.That(openGames[0].HostName, Is.EqualTo("Host1"));
    }

    // ==================== CancelGame ====================

    [Test]
    public async Task CancelGame_WhenHostCancels_SendsGameCancelledAndLobbyUpdate()
    {
        await _hub.CreateGame("Host");

        // Clear invocations from CreateGame
        _callerMock.Invocations.Clear();
        _allMock.Invocations.Clear();

        await _hub.CancelGame();

        _callerMock.Verify(c => c.SendCoreAsync(
            "GameCancelled",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _allMock.Verify(c => c.SendCoreAsync(
            "LobbyUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CancelGame_WhenNotHost_SendsCancelFailed()
    {
        // Create game as test-conn-id
        await _hub.CreateGame("Host");

        // Switch to different connection
        _contextMock.Setup(c => c.ConnectionId).Returns("other-conn");
        _callerMock.Invocations.Clear();

        await _hub.CancelGame();

        _callerMock.Verify(c => c.SendCoreAsync(
            "CancelFailed",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==================== CheckPendingGame ====================

    [Test]
    public async Task CheckPendingGame_ValidToken_SendsPendingGameFound()
    {
        await _hub.CreateGame("Host");
        var gameId = _gameManager.GetOpenGames()[0].GameId;

        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("my-token"))
            .ReturnsAsync(new ReconnectSession
            {
                Token = "my-token",
                GameId = gameId,
                PlayerNumber = 1,
                PlayerName = "Host"
            });

        _callerMock.Invocations.Clear();

        await _hub.CheckPendingGame("my-token");

        _callerMock.Verify(c => c.SendCoreAsync(
            "PendingGameFound",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CheckPendingGame_InvalidToken_SendsNoPendingGame()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("bad-token"))
            .ReturnsAsync((ReconnectSession?)null);

        await _hub.CheckPendingGame("bad-token");

        _callerMock.Verify(c => c.SendCoreAsync(
            "NoPendingGame",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==================== Reconnect ====================

    [Test]
    public async Task Reconnect_InvalidToken_SendsReconnectFailed()
    {
        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("expired"))
            .ReturnsAsync((ReconnectSession?)null);

        await _hub.Reconnect("expired");

        _callerMock.Verify(c => c.SendCoreAsync(
            "ReconnectFailed",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Reconnect_ValidToken_JoinsGroupAndSendsReconnected()
    {
        // Create and join a game
        await _hub.CreateGame("Player1");
        var gameId = _gameManager.GetOpenGames()[0].GameId;

        _contextMock.Setup(c => c.ConnectionId).Returns("conn-2");
        SetupAuthenticatedUser(Guid.NewGuid());
        await _hub.JoinGame(gameId, "Player2");

        // Now simulate reconnect
        var newConnId = "new-conn-id";
        _contextMock.Setup(c => c.ConnectionId).Returns(newConnId);

        _gameStateRepoMock.Setup(r => r.GetReconnectSessionAsync("valid-token"))
            .ReturnsAsync(new ReconnectSession
            {
                Token = "valid-token",
                GameId = gameId,
                PlayerNumber = 1,
                PlayerName = "Player1"
            });

        _gameStateRepoMock.Setup(r => r.GetPlayersConnectionStateAsync(gameId))
            .ReturnsAsync(new PlayerConnectionState
            {
                Player1Connected = true,
                Player2Connected = true
            });

        _callerMock.Invocations.Clear();
        _groupsMock.Invocations.Clear();

        await _hub.Reconnect("valid-token");

        _groupsMock.Verify(g => g.AddToGroupAsync(
            newConnId,
            gameId,
            It.IsAny<CancellationToken>()), Times.Once);

        _callerMock.Verify(c => c.SendCoreAsync(
            "Reconnected",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==================== MovePaddle ====================

    [Test]
    public async Task MovePaddle_UpdatesPaddlePosition()
    {
        await _hub.CreateGame("Player1");
        var gameId = _gameManager.GetOpenGames()[0].GameId;

        await _hub.MovePaddle(200.0);

        var game = _gameManager.GetGame(gameId);
        Assert.That(game!.Paddle1.Y, Is.EqualTo(200.0));
    }

    [Test]
    public void MovePaddle_UnknownConnection_DoesNotThrow()
    {
        _contextMock.Setup(c => c.ConnectionId).Returns("unknown-conn");

        Assert.DoesNotThrowAsync(() => _hub.MovePaddle(100.0));
    }

    // ==================== OnDisconnectedAsync ====================

    [Test]
    public async Task OnDisconnectedAsync_RemovesWaitingGame()
    {
        await _hub.CreateGame("Player1");
        var gameId = _gameManager.GetOpenGames()[0].GameId;

        await _hub.OnDisconnectedAsync(null);

        Assert.That(_gameManager.GetGame(gameId), Is.Null);
    }
}
