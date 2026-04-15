using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using NppApi.Services;
using NppCore.Models;

namespace NppApi.Hubs;

public class GameHub : Hub
{
    private readonly GameManagerService _gameManager;
    private readonly ILogger<GameHub> _logger;

    public GameHub(GameManagerService gameManager, ILogger<GameHub> logger)
    {
        _gameManager = gameManager;
        _logger = logger;
    }

    public async Task CreateGame(string playerName)
    {
        try
        {
            var playerId = GetPlayerIdFromClaims();
            var (game, token) = await _gameManager.CreateGameAsync(Context.ConnectionId, playerId, playerName);
            await Groups.AddToGroupAsync(Context.ConnectionId, game.Id);

            if (!string.IsNullOrEmpty(token))
            {
                await Clients.Caller.SendAsync("ReconnectToken", new ReconnectTokenDto
                {
                    Token = token,
                    GameId = game.Id,
                    PlayerNumber = 1
                });
            }

            await Clients.All.SendAsync("LobbyUpdated", _gameManager.GetOpenGames());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating game for player {PlayerName}", playerName);
            throw new HubException("Failed to create game. Please try again.");
        }
    }

    public async Task JoinGame(string gameId, string playerName)
    {
        try
        {
            var playerId = GetPlayerIdFromClaims();
            var (game, token) = await _gameManager.JoinGameAsync(gameId, Context.ConnectionId, playerId, playerName);
            if (game == null)
            {
                await Clients.Caller.SendAsync("JoinFailed", "Game not found or already started");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

            if (!string.IsNullOrEmpty(token))
            {
                await Clients.Caller.SendAsync("ReconnectToken", new ReconnectTokenDto
                {
                    Token = token,
                    GameId = game.Id,
                    PlayerNumber = 2
                });
            }

            await Clients.All.SendAsync("LobbyUpdated", _gameManager.GetOpenGames());

            var stateDto = new GameStateDto
            {
                GameId = game.Id,
                Ball = new BallDto { X = game.Ball.X, Y = game.Ball.Y },
                Paddle1 = new PaddleDto { Y = game.Paddle1.Y },
                Paddle2 = new PaddleDto { Y = game.Paddle2.Y },
                Player1 = game.Player1 != null ? new PlayerDto { Name = game.Player1.Name, Score = game.Player1.Score } : null,
                Player2 = game.Player2 != null ? new PlayerDto { Name = game.Player2.Name, Score = game.Player2.Score } : null,
                State = game.State.ToString()
            };

            await Clients.Group(gameId).SendAsync("GameStarted", stateDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining game {GameId} for player {PlayerName}", gameId, playerName);
            throw new HubException("Failed to join game. Please try again.");
        }
    }

    public async Task CheckPendingGame(string token)
    {
        var session = await _gameManager.CheckPendingGameAsync(token);
        if (session != null)
        {
            await Clients.Caller.SendAsync("PendingGameFound", new
            {
                gameId = session.GameId,
                playerNumber = session.PlayerNumber,
                playerName = session.PlayerName
            });
        }
        else
        {
            await Clients.Caller.SendAsync("NoPendingGame");
        }
    }

    public async Task Reconnect(string token)
    {
        var result = await _gameManager.ReconnectAsync(token, Context.ConnectionId);

        if (result.Success && result.GameId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, result.GameId);

            var game = _gameManager.GetGame(result.GameId);
            if (game != null)
            {
                var stateDto = new GameStateDto
                {
                    GameId = game.Id,
                    Ball = new BallDto { X = game.Ball.X, Y = game.Ball.Y },
                    Paddle1 = new PaddleDto { Y = game.Paddle1.Y },
                    Paddle2 = new PaddleDto { Y = game.Paddle2.Y },
                    Player1 = game.Player1 != null ? new PlayerDto { Name = game.Player1.Name, Score = game.Player1.Score } : null,
                    Player2 = game.Player2 != null ? new PlayerDto { Name = game.Player2.Name, Score = game.Player2.Score } : null,
                    State = game.State.ToString()
                };

                await Clients.Caller.SendAsync("Reconnected", new
                {
                    success = true,
                    gameId = result.GameId,
                    playerNumber = result.PlayerNumber,
                    playerName = result.PlayerName,
                    gameState = stateDto
                });

                if (game.State == GameState.Playing && _gameManager.BothPlayersConnected(result.GameId))
                {
                    await Clients.Group(result.GameId).SendAsync("GameResumed", stateDto);
                }
            }
        }
        else
        {
            await Clients.Caller.SendAsync("ReconnectFailed", result.ErrorMessage ?? "Reconnection failed");
        }
    }

    public async Task GetLobby()
    {
        try
        {
            await Clients.Caller.SendAsync("LobbyUpdated", _gameManager.GetOpenGames());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lobby");
            throw new HubException("Failed to get lobby. Please try again.");
        }
    }

    public async Task CancelGame()
    {
        try
        {
            var success = await _gameManager.CancelGameAsync(Context.ConnectionId);

            if (success)
            {
                await Clients.Caller.SendAsync("GameCancelled");
                await Clients.All.SendAsync("LobbyUpdated", _gameManager.GetOpenGames());
            }
            else
            {
                await Clients.Caller.SendAsync("CancelFailed", "Unable to cancel game");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling game for connection {ConnectionId}", Context.ConnectionId);
            throw new HubException("Failed to cancel game. Please try again.");
        }
    }

    public Task MovePaddle(double y)
    {
        try
        {
            _gameManager.UpdatePaddle(Context.ConnectionId, y);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving paddle for connection {ConnectionId}", Context.ConnectionId);
            // Don't throw HubException for paddle update as it's a frequent operation
            return Task.CompletedTask;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _gameManager.HandlePlayerDisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetPlayerIdFromClaims()
    {
        var playerIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(playerIdClaim) || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            throw new HubException("User is not authenticated or PlayerId is invalid");
        }

        return playerId;
    }
}
