using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NppApi.Hubs;
using NppCore.Constants;
using NppCore.Models;
using Microsoft.Extensions.DependencyInjection;
using NppCore.Services.Features.Match;
using NppCore.Services.Persistence.Redis;
using NppCore.Services.Features.Player;

namespace NppApi.Services;

public class GameManagerService : IHostedService, IDisposable
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, string> _playerGameMap = new();
    private readonly ConcurrentDictionary<string, DateTime> _pausedGameTimeouts = new();
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IGameStateRepository _gameStateRepository;
    private readonly ILogger<GameManagerService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private Timer? _gameLoopTimer;
    private Timer? _timeoutCheckTimer;
    private readonly Random _random = new();
    private int _frameCount;
    private const int SyncInterval = 5;
    private static readonly TimeSpan ReconnectTokenExpiry = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TimeoutCheckInterval = TimeSpan.FromSeconds(30);

    public GameManagerService(
        IHubContext<GameHub> hubContext,
        IGameStateRepository gameStateRepository,
        ILogger<GameManagerService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _hubContext = hubContext;
        _gameStateRepository = gameStateRepository;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RecoverStateFromRedisAsync();
        _gameLoopTimer = new Timer(GameLoop, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000.0 / 60));
        _timeoutCheckTimer = new Timer(CheckTimeouts, null, TimeoutCheckInterval, TimeoutCheckInterval);
    }

    private async Task RecoverStateFromRedisAsync()
    {
        try
        {
            var games = await _gameStateRepository.LoadAllGamesAsync();
            var playerMappings = await _gameStateRepository.LoadAllPlayerMappingsAsync();

            foreach (var game in games)
            {
                // Only recover games that are still active (not finished)
                if (game.State != GameState.Finished)
                {
                    // Mark recovered playing games as paused since connections are stale
                    if (game.State == GameState.Playing)
                    {
                        game.State = GameState.Paused;
                        _pausedGameTimeouts[game.Id] = DateTime.UtcNow.Add(ReconnectTokenExpiry);

                        // Mark both players as disconnected
                        await _gameStateRepository.MoveToPausedGamesAsync(game.Id);
                        await _gameStateRepository.SetPlayerConnectedAsync(game.Id, 1, false, null);
                        if (game.Player2 != null)
                        {
                            await _gameStateRepository.SetPlayerConnectedAsync(game.Id, 2, false, null);
                        }
                    }

                    _games[game.Id] = game;
                }
            }

            // Don't restore player mappings since connection IDs are stale after restart
            // Players will need to reconnect using their tokens

            _logger.LogInformation("Recovered {GameCount} games from Redis (all marked as paused for reconnection)",
                _games.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to recover state from Redis, starting fresh");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gameLoopTimer?.Change(Timeout.Infinite, 0);
        _timeoutCheckTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _gameLoopTimer?.Dispose();
        _timeoutCheckTimer?.Dispose();
    }

    public async Task<(Game game, string token)> CreateGameAsync(string connectionId, Guid playerId, string playerName)
    {
        var gameId = Guid.NewGuid().ToString()[..8];
        var game = new Game
        {
            Id = gameId,
            Player1 = new Player { ConnectionId = connectionId, PlayerId = playerId, Name = playerName },
            State = GameState.WaitingForPlayer
        };

        InitializeBall(game);
        InitializePaddles(game);

        _games[gameId] = game;
        _playerGameMap[connectionId] = gameId;

        try
        {
            await _gameStateRepository.CreateGameAsync(game);
            await _gameStateRepository.SetPlayerGameMappingAsync(connectionId, gameId);
            await _gameStateRepository.SetPlayerConnectedAsync(gameId, 1, true, connectionId);

            var token = await _gameStateRepository.CreateReconnectTokenAsync(
                gameId, 1, playerName, ReconnectTokenExpiry);

            return (game, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist game creation to Redis for game {GameId}", gameId);
            // Return empty token on error - game still works in memory
            return (game, string.Empty);
        }
    }

    public async Task<(Game? game, string? token)> JoinGameAsync(string gameId, string connectionId, Guid playerId, string playerName)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (null, null);

        if (game.State != GameState.WaitingForPlayer)
            return (null, null);

        var player2 = new Player { ConnectionId = connectionId, PlayerId = playerId, Name = playerName };
        game.Player2 = player2;
        game.State = GameState.Playing;
        _playerGameMap[connectionId] = gameId;

        try
        {
            await _gameStateRepository.JoinGameAsync(gameId, player2);
            await _gameStateRepository.SetPlayerGameMappingAsync(connectionId, gameId);
            await _gameStateRepository.SetPlayerConnectedAsync(gameId, 2, true, connectionId);

            var token = await _gameStateRepository.CreateReconnectTokenAsync(
                gameId, 2, playerName, ReconnectTokenExpiry);

            return (game, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist game join to Redis for game {GameId}", gameId);
            return (game, string.Empty);
        }
    }

    public async Task<ReconnectResultDto> ReconnectAsync(string token, string newConnectionId)
    {
        try
        {
            var session = await _gameStateRepository.GetReconnectSessionAsync(token);
            if (session == null)
            {
                return new ReconnectResultDto
                {
                    Success = false,
                    ErrorMessage = "Invalid or expired token"
                };
            }

            if (!_games.TryGetValue(session.GameId, out var game))
            {
                // Try to load from Redis if not in memory
                var games = await _gameStateRepository.LoadAllGamesAsync();
                game = games.FirstOrDefault(g => g.Id == session.GameId);

                if (game == null)
                {
                    await _gameStateRepository.RemoveReconnectTokenAsync(token);
                    return new ReconnectResultDto
                    {
                        Success = false,
                        ErrorMessage = "Game no longer exists"
                    };
                }

                _games[session.GameId] = game;
            }

            // Update the player's connection ID
            if (session.PlayerNumber == 1 && game.Player1 != null)
            {
                game.Player1.ConnectionId = newConnectionId;
            }
            else if (session.PlayerNumber == 2 && game.Player2 != null)
            {
                game.Player2.ConnectionId = newConnectionId;
            }
            else
            {
                return new ReconnectResultDto
                {
                    Success = false,
                    ErrorMessage = "Player not found in game"
                };
            }

            _playerGameMap[newConnectionId] = session.GameId;

            // Update Redis
            await _gameStateRepository.UpdatePlayerConnectionIdAsync(session.GameId, session.PlayerNumber, newConnectionId);
            await _gameStateRepository.SetPlayerConnectedAsync(session.GameId, session.PlayerNumber, true, newConnectionId);
            await _gameStateRepository.SetPlayerGameMappingAsync(newConnectionId, session.GameId);

            // Check if both players are now connected
            var connectionState = await _gameStateRepository.GetPlayersConnectionStateAsync(session.GameId);

            if (game.State == GameState.Paused && connectionState.Player1Connected && connectionState.Player2Connected)
            {
                game.State = GameState.Playing;
                await _gameStateRepository.MoveFromPausedToPlayingAsync(session.GameId);
                _pausedGameTimeouts.TryRemove(session.GameId, out _);

                _logger.LogInformation("Game {GameId} resumed - both players reconnected", session.GameId);
            }

            _logger.LogInformation("Player {PlayerNumber} reconnected to game {GameId}", session.PlayerNumber, session.GameId);

            return new ReconnectResultDto
            {
                Success = true,
                GameId = session.GameId,
                PlayerNumber = session.PlayerNumber,
                PlayerName = session.PlayerName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reconnection with token {Token}", token);
            return new ReconnectResultDto
            {
                Success = false,
                ErrorMessage = "Reconnection failed"
            };
        }
    }

    public async Task<ReconnectSession?> CheckPendingGameAsync(string token)
    {
        try
        {
            var session = await _gameStateRepository.GetReconnectSessionAsync(token);
            if (session == null)
                return null;

            // Verify the game still exists
            if (!_games.ContainsKey(session.GameId))
            {
                // Try to find in Redis
                var exists = await _gameStateRepository.GetPlayersConnectionStateAsync(session.GameId);
                if (exists.Player1ConnectionId == null && exists.Player2ConnectionId == null)
                {
                    await _gameStateRepository.RemoveReconnectTokenAsync(token);
                    return null;
                }
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking pending game for token {Token}", token);
            return null;
        }
    }

    public async Task HandlePlayerDisconnectAsync(string connectionId)
    {
        if (!_playerGameMap.TryGetValue(connectionId, out var gameId))
            return;

        if (!_games.TryGetValue(gameId, out var game))
            return;

        var playerNumber = game.Player1?.ConnectionId == connectionId ? 1 : 2;
        var otherPlayerNumber = playerNumber == 1 ? 2 : 1;
        var otherPlayer = playerNumber == 1 ? game.Player2 : game.Player1;

        _playerGameMap.TryRemove(connectionId, out _);

        // For waiting games, just remove them
        if (game.State == GameState.WaitingForPlayer)
        {
            _games.TryRemove(gameId, out _);

            RunBackgroundTask(async () =>
            {
                await _gameStateRepository.RemoveGameAsync(gameId);
                await _gameStateRepository.RemovePlayerMappingAsync(connectionId);
                await _gameStateRepository.RemoveGameTokensAsync(gameId);
            }, $"CleanupWaitingGame-{gameId}");

            await _hubContext.Clients.All.SendAsync("LobbyUpdated", GetOpenGames());
            return;
        }

        // For playing/paused games, pause and wait for reconnection
        try
        {
            await _gameStateRepository.SetPlayerConnectedAsync(gameId, playerNumber, false, null);
            await _gameStateRepository.RemovePlayerMappingAsync(connectionId);

            if (game.State == GameState.Playing)
            {
                game.State = GameState.Paused;
                await _gameStateRepository.MoveToPausedGamesAsync(gameId);
                _pausedGameTimeouts[gameId] = DateTime.UtcNow.Add(ReconnectTokenExpiry);

                _logger.LogInformation("Game {GameId} paused - player {PlayerNumber} disconnected", gameId, playerNumber);
            }

            // Notify the other player if connected
            if (otherPlayer != null)
            {
                var connectionState = await _gameStateRepository.GetPlayersConnectionStateAsync(gameId);
                var isOtherConnected = otherPlayerNumber == 1
                    ? connectionState.Player1Connected
                    : connectionState.Player2Connected;

                if (isOtherConnected)
                {
                    await _hubContext.Clients.Client(otherPlayer.ConnectionId).SendAsync("OpponentDisconnected");
                    await _hubContext.Clients.Client(otherPlayer.ConnectionId).SendAsync("GamePaused");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle player disconnect for game {GameId}", gameId);
        }
    }

    private async void CheckTimeouts(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredGames = _pausedGameTimeouts
            .Where(kvp => kvp.Value <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var gameId in expiredGames)
        {
            _pausedGameTimeouts.TryRemove(gameId, out _);

            if (_games.TryRemove(gameId, out var game))
            {
                _logger.LogInformation("Game {GameId} timed out - cleaning up", gameId);

                // Notify any connected players
                if (game.Player1 != null && _playerGameMap.ContainsKey(game.Player1.ConnectionId))
                {
                    _playerGameMap.TryRemove(game.Player1.ConnectionId, out _);
                    await _hubContext.Clients.Client(game.Player1.ConnectionId)
                        .SendAsync("GameEnded", "Game timed out - opponent did not reconnect");
                }

                if (game.Player2 != null && _playerGameMap.ContainsKey(game.Player2.ConnectionId))
                {
                    _playerGameMap.TryRemove(game.Player2.ConnectionId, out _);
                    await _hubContext.Clients.Client(game.Player2.ConnectionId)
                        .SendAsync("GameEnded", "Game timed out - opponent did not reconnect");
                }

                // Clean up Redis
                try
                {
                    await _gameStateRepository.RemoveGameAsync(gameId);
                    await _gameStateRepository.RemoveGameTokensAsync(gameId);

                    if (game.Player1 != null)
                        await _gameStateRepository.RemovePlayerMappingAsync(game.Player1.ConnectionId);
                    if (game.Player2 != null)
                        await _gameStateRepository.RemovePlayerMappingAsync(game.Player2.ConnectionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean up timed out game {GameId}", gameId);
                }
            }
        }
    }

    public List<LobbyGameDto> GetOpenGames()
    {
        return _games.Values
            .Where(g => g.State == GameState.WaitingForPlayer && g.Player1 != null)
            .Select(g => new LobbyGameDto
            {
                GameId = g.Id,
                HostName = g.Player1!.Name
            })
            .ToList();
    }

    public async Task<bool> CancelGameAsync(string connectionId)
    {
        if (!_playerGameMap.TryGetValue(connectionId, out var gameId))
            return false;

        if (!_games.TryGetValue(gameId, out var game))
            return false;

        if (game.State != GameState.WaitingForPlayer)
            return false;

        if (game.Player1?.ConnectionId != connectionId)
            return false;

        _games.TryRemove(gameId, out _);
        _playerGameMap.TryRemove(connectionId, out _);

        RunBackgroundTask(async () =>
        {
            await _gameStateRepository.RemoveGameAsync(gameId);
            await _gameStateRepository.RemovePlayerMappingAsync(connectionId);
            await _gameStateRepository.RemoveGameTokensAsync(gameId);
        }, $"CleanupCancelledGame-{gameId}");

        _logger.LogInformation("Game {GameId} cancelled by host {ConnectionId}", gameId, connectionId);
        return true;
    }

    public void UpdatePaddle(string connectionId, double y)
    {
        if (!_playerGameMap.TryGetValue(connectionId, out var gameId))
            return;

        if (!_games.TryGetValue(gameId, out var game))
            return;

        // Validate input
        if (double.IsNaN(y) || double.IsInfinity(y))
            return;

        y = Math.Clamp(y, 0, Game.CanvasHeight - Game.PaddleHeight);

        if (game.Player1?.ConnectionId == connectionId)
            game.Paddle1.Y = y;
        else if (game.Player2?.ConnectionId == connectionId)
            game.Paddle2.Y = y;
    }

    public void RemovePlayer(string connectionId)
    {
        // This method is kept for backwards compatibility but now delegates to async version
        _ = HandlePlayerDisconnectAsync(connectionId);
    }

    public string? GetGameId(string connectionId)
    {
        _playerGameMap.TryGetValue(connectionId, out var gameId);
        return gameId;
    }

    public Game? GetGame(string gameId)
    {
        _games.TryGetValue(gameId, out var game);
        return game;
    }

    public bool BothPlayersConnected(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return false;

        return game.Player1 != null && game.Player2 != null &&
               _playerGameMap.ContainsKey(game.Player1.ConnectionId) &&
               _playerGameMap.ContainsKey(game.Player2.ConnectionId);
    }

    private void InitializeBall(Game game)
    {
        game.Ball.X = Game.CanvasWidth / 2.0;
        game.Ball.Y = Game.CanvasHeight / 2.0;

        var angle = (_random.NextDouble() - 0.5) * Math.PI / 2;
        var direction = _random.Next(2) == 0 ? 1 : -1;
        var speed = (double)GameConstants.InitialBallSpeed;

        game.Ball.VelocityX = Math.Cos(angle) * speed * direction;
        game.Ball.VelocityY = Math.Sin(angle) * speed;
    }

    private void InitializePaddles(Game game)
    {
        game.Paddle1.Y = (Game.CanvasHeight - Game.PaddleHeight) / 2.0;
        game.Paddle2.Y = (Game.CanvasHeight - Game.PaddleHeight) / 2.0;
    }

    private async void GameLoop(object? state)
    {
        try
        {
        _frameCount++;
        var shouldSync = _frameCount % SyncInterval == 0;

        // Only process games that are actively playing (skip paused games)
        foreach (var game in _games.Values.Where(g => g.State == GameState.Playing))
        {
            UpdateBall(game);
            BroadcastGameState(game);

            // Periodic sync to Redis every N frames
            if (shouldSync)
            {
                var gameId = game.Id;
                var gameCopy = game; // Capture for closure
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _gameStateRepository.SyncGameStateAsync(gameId, gameCopy);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to sync game state to Redis for game {GameId}", gameId);
                    }
                });
            }
        }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in game loop");
        }
    }

    private void UpdateBall(Game game)
    {
        game.Ball.X += game.Ball.VelocityX;
        game.Ball.Y += game.Ball.VelocityY;

        // Top/bottom wall bounce
        if (game.Ball.Y <= 0 || game.Ball.Y >= Game.CanvasHeight - Game.BallSize)
        {
            game.Ball.VelocityY = -game.Ball.VelocityY;
            game.Ball.Y = Math.Clamp(game.Ball.Y, 0, Game.CanvasHeight - Game.BallSize);
        }

        // Paddle 1 collision (left)
        if (game.Ball.X <= Game.PaddleOffset + Game.PaddleWidth &&
            game.Ball.Y + Game.BallSize >= game.Paddle1.Y &&
            game.Ball.Y <= game.Paddle1.Y + Game.PaddleHeight &&
            game.Ball.VelocityX < 0)
        {
            game.Ball.VelocityX = -game.Ball.VelocityX * GameConstants.BallSpeedMultiplier;
            var hitPos = (game.Ball.Y - game.Paddle1.Y) / Game.PaddleHeight;
            game.Ball.VelocityY = (hitPos - 0.5) * GameConstants.MaxBallVerticalSpeed;
            game.Ball.X = Game.PaddleOffset + Game.PaddleWidth;
        }

        // Paddle 2 collision (right)
        if (game.Ball.X >= Game.CanvasWidth - Game.PaddleOffset - Game.PaddleWidth - Game.BallSize &&
            game.Ball.Y + Game.BallSize >= game.Paddle2.Y &&
            game.Ball.Y <= game.Paddle2.Y + Game.PaddleHeight &&
            game.Ball.VelocityX > 0)
        {
            game.Ball.VelocityX = -game.Ball.VelocityX * GameConstants.BallSpeedMultiplier;
            var hitPos = (game.Ball.Y - game.Paddle2.Y) / Game.PaddleHeight;
            game.Ball.VelocityY = (hitPos - 0.5) * GameConstants.MaxBallVerticalSpeed;
            game.Ball.X = Game.CanvasWidth - Game.PaddleOffset - Game.PaddleWidth - Game.BallSize;
        }

        // Score
        if (game.Ball.X < 0)
        {
            if (game.Player2 != null)
            {
                game.Player2.Score++;
                CheckWin(game);
            }
            InitializeBall(game);
        }
        else if (game.Ball.X > Game.CanvasWidth)
        {
            if (game.Player1 != null)
            {
                game.Player1.Score++;
                CheckWin(game);
            }
            InitializeBall(game);
        }
    }

    private void CheckWin(Game game)
    {
        if (game.Player1 == null || game.Player2 == null)
            return;

        // Guard against multiple calls
        if (game.State == GameState.Finished)
            return;

        if (game.Player1.Score >= Game.WinScore || game.Player2.Score >= Game.WinScore)
        {
            game.State = GameState.Finished;

            var winner = game.Player1.Score >= Game.WinScore ? game.Player1 : game.Player2;
            var loser = winner == game.Player1 ? game.Player2 : game.Player1;
            Guid matchId = Guid.NewGuid();
            var matchTime = DateTimeOffset.UtcNow;

            // Save match records for both players (winner's perspective and loser's perspective)
            RunBackgroundTask(() => SaveMatchToDb(winner.Name, winner, loser, matchId, matchTime), "SaveMatchForWinner");
            RunBackgroundTask(() => SaveMatchToDb(winner.Name, loser, winner, matchId, matchTime), "SaveMatchForLoser");
            RunBackgroundTask(() => SaveHistoryToDb(winner.Name, winner, loser, matchId, matchTime), "SaveHistory");
            RunBackgroundTask(() => UpdatePlayerStatsAsync(winner, loser, matchId, matchTime), "UpdateStats");

            _ = _hubContext.Clients.Client(game.Player1.ConnectionId)
                .SendAsync("GameEnded", winner.Name);
            _ = _hubContext.Clients.Client(game.Player2.ConnectionId)
                .SendAsync("GameEnded", winner.Name);

            _games.TryRemove(game.Id, out _);
            _pausedGameTimeouts.TryRemove(game.Id, out _);
        }
    }


    private async Task UpdatePlayerStatsAsync(Player winner, Player loser, Guid matchId, DateTimeOffset matchTime)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();

        try
        {
            await playerStatsService.UpdateStatsAfterMatchAsync(
                winner.PlayerId,
                winner.Name,
                winner.Score,
                loser.PlayerId,
                loser.Name,
                loser.Score,
                matchId,
                matchTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player stats");
        }
    }

    private void RunBackgroundTask(Func<Task> task, string operationName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await task();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background task failed: {Operation}", operationName);
            }
        });
    }

    private void BroadcastGameState(Game game)
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

        if (game.Player1 != null)
            _ = _hubContext.Clients.Client(game.Player1.ConnectionId).SendAsync("GameStateUpdated", stateDto);
        if (game.Player2 != null)
            _ = _hubContext.Clients.Client(game.Player2.ConnectionId).SendAsync("GameStateUpdated", stateDto);
    }

    private async Task SaveMatchToDb(string winnerName, Player p1, Player p2, Guid matchId, DateTimeOffset matchTime)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var matchService = scope.ServiceProvider.GetRequiredService<IPlayerMatchesService>();
        var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();

        var t1 = playerService.GetByUsernameAsync(p1.Name);
        var t2 = playerService.GetByUsernameAsync(p2.Name);

        var results = await Task.WhenAll(t1, t2);
        var player1 = results[0];
        var player2 = results[1];

        if (player1 == null || player2 == null)
        {
            _logger.LogError("Player not found in database: {Player1} or {Player2}", p1.Name, p2.Name);
            return;
        }

        string score = $"{p1.Score}:{p2.Score}";
        string result = (p1.Name == winnerName) ? GameConstants.ResultWin : GameConstants.ResultLoss;

        await matchService.CreateAsync(
            player1.PlayerId,
            player2.PlayerId,
            matchTime.Year.ToString(),
            matchTime,
            matchId,
            p2.Name,
            score,
            result
        );
    }

    private async Task SaveHistoryToDb(string winnerName, Player p1, Player p2, Guid matchId, DateTimeOffset matchTime)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var matchService = scope.ServiceProvider.GetRequiredService<IPlayerMatchesService>();
        string score = $"{p1.Score}:{p2.Score}";

        await matchService.CreateHistoryAsync(matchTime, p1.Name, p2.Name, score, winnerName, matchId);
    }
}
