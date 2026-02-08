using System.Globalization;
using Microsoft.Extensions.Logging;
using NppCore.Models;

namespace NppCore.Services.Persistence.Redis;

public class RedisGameStateRepository : IGameStateRepository
{
    private readonly IRedisService _redis;
    private readonly ILogger<RedisGameStateRepository> _logger;

    private const string GameKeyPrefix = "game:";
    private const string PlayerGameKeyPrefix = "player:game:";
    private const string OpenGamesKey = "games:open";
    private const string PlayingGamesKey = "games:playing";
    private const string PausedGamesKey = "games:paused";
    private const string ReconnectTokenKeyPrefix = "reconnect:";
    private const string GameTokensKeyPrefix = "game:tokens:";

    public RedisGameStateRepository(IRedisService redis, ILogger<RedisGameStateRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task CreateGameAsync(Game game)
    {
        var key = GameKeyPrefix + game.Id;
        var fields = SerializeGame(game);

        await _redis.HashSetAsync(key, fields);
        await _redis.SetAddAsync(OpenGamesKey, game.Id);

        _logger.LogDebug("Created game {GameId} in Redis", game.Id);
    }

    public async Task<bool> JoinGameAsync(string gameId, Player player2)
    {
        var key = GameKeyPrefix + gameId;

        var fields = new Dictionary<string, string>
        {
            ["player2_id"] = player2.ConnectionId,
            ["player2_name"] = player2.Name,
            ["player2_score"] = "0",
            ["state"] = GameState.Playing.ToString()
        };

        await _redis.HashSetAsync(key, fields);
        await MoveToPlayingGamesAsync(gameId);

        _logger.LogDebug("Player {PlayerId} joined game {GameId} in Redis", player2.ConnectionId, gameId);
        return true;
    }

    public async Task RemoveGameAsync(string gameId)
    {
        var key = GameKeyPrefix + gameId;

        await _redis.KeyDeleteAsync(key);
        await _redis.SetRemoveAsync(OpenGamesKey, gameId);
        await _redis.SetRemoveAsync(PlayingGamesKey, gameId);
        await _redis.SetRemoveAsync(PausedGamesKey, gameId);

        _logger.LogDebug("Removed game {GameId} from Redis", gameId);
    }

    public async Task SetPlayerGameMappingAsync(string connectionId, string gameId)
    {
        var key = PlayerGameKeyPrefix + connectionId;
        await _redis.StringSetAsync(key, gameId);
    }

    public async Task<string?> GetGameIdByPlayerAsync(string connectionId)
    {
        var key = PlayerGameKeyPrefix + connectionId;
        return await _redis.StringGetAsync(key);
    }

    public async Task RemovePlayerMappingAsync(string connectionId)
    {
        var key = PlayerGameKeyPrefix + connectionId;
        await _redis.KeyDeleteAsync(key);
    }

    public async Task SyncGameStateAsync(string gameId, Game game)
    {
        var key = GameKeyPrefix + gameId;
        var fields = new Dictionary<string, string>
        {
            ["ball_x"] = game.Ball.X.ToString(CultureInfo.InvariantCulture),
            ["ball_y"] = game.Ball.Y.ToString(CultureInfo.InvariantCulture),
            ["ball_vx"] = game.Ball.VelocityX.ToString(CultureInfo.InvariantCulture),
            ["ball_vy"] = game.Ball.VelocityY.ToString(CultureInfo.InvariantCulture),
            ["paddle1_y"] = game.Paddle1.Y.ToString(CultureInfo.InvariantCulture),
            ["paddle2_y"] = game.Paddle2.Y.ToString(CultureInfo.InvariantCulture),
            ["player1_score"] = game.Player1?.Score.ToString() ?? "0",
            ["player2_score"] = game.Player2?.Score.ToString() ?? "0",
            ["state"] = game.State.ToString()
        };

        await _redis.HashSetAsync(key, fields);
    }

    public async Task<List<Game>> LoadAllGamesAsync()
    {
        var games = new List<Game>();

        var gameKeys = await _redis.KeysAsync(GameKeyPrefix + "*");

        foreach (var key in gameKeys)
        {
            try
            {
                var fields = await _redis.HashGetAllAsync(key);
                if (fields.Count == 0) continue;

                var game = DeserializeGame(fields);
                if (game != null)
                {
                    games.Add(game);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load game from key {Key}", key);
            }
        }

        _logger.LogInformation("Loaded {Count} games from Redis", games.Count);
        return games;
    }

    public async Task<Dictionary<string, string>> LoadAllPlayerMappingsAsync()
    {
        var mappings = new Dictionary<string, string>();

        var playerKeys = await _redis.KeysAsync(PlayerGameKeyPrefix + "*");

        foreach (var key in playerKeys)
        {
            var connectionId = key.Replace(PlayerGameKeyPrefix, "");
            var gameId = await _redis.StringGetAsync(key);

            if (!string.IsNullOrEmpty(gameId))
            {
                mappings[connectionId] = gameId;
            }
        }

        _logger.LogInformation("Loaded {Count} player mappings from Redis", mappings.Count);
        return mappings;
    }

    public async Task<List<string>> GetOpenGameIdsAsync()
    {
        return await _redis.SetMembersAsync(OpenGamesKey);
    }

    public async Task AddToOpenGamesAsync(string gameId)
    {
        await _redis.SetAddAsync(OpenGamesKey, gameId);
    }

    public async Task RemoveFromOpenGamesAsync(string gameId)
    {
        await _redis.SetRemoveAsync(OpenGamesKey, gameId);
    }

    public async Task MoveToPlayingGamesAsync(string gameId)
    {
        await _redis.SetRemoveAsync(OpenGamesKey, gameId);
        await _redis.SetAddAsync(PlayingGamesKey, gameId);
    }

    // Reconnection token methods
    public async Task<string> CreateReconnectTokenAsync(string gameId, int playerNumber, string playerName, TimeSpan expiry)
    {
        var token = Guid.NewGuid().ToString();
        var tokenKey = ReconnectTokenKeyPrefix + token;
        var gameTokensKey = GameTokensKeyPrefix + gameId;

        var fields = new Dictionary<string, string>
        {
            ["game_id"] = gameId,
            ["player_number"] = playerNumber.ToString(),
            ["player_name"] = playerName,
            ["expires_at"] = DateTime.UtcNow.Add(expiry).ToString("O")
        };

        await _redis.HashSetAsync(tokenKey, fields);
        await _redis.KeyExpireAsync(tokenKey, expiry);
        await _redis.SetAddAsync(gameTokensKey, token);

        // Store token in game hash as well
        var gameKey = GameKeyPrefix + gameId;
        await _redis.HashSetAsync(gameKey, $"player{playerNumber}_token", token);

        _logger.LogDebug("Created reconnect token {Token} for player {PlayerNumber} in game {GameId}", token, playerNumber, gameId);
        return token;
    }

    public async Task<ReconnectSession?> GetReconnectSessionAsync(string token)
    {
        var tokenKey = ReconnectTokenKeyPrefix + token;
        var fields = await _redis.HashGetAllAsync(tokenKey);

        if (fields.Count == 0)
            return null;

        if (!fields.TryGetValue("game_id", out var gameId) ||
            !fields.TryGetValue("player_number", out var playerNumberStr) ||
            !fields.TryGetValue("player_name", out var playerName))
            return null;

        return new ReconnectSession
        {
            Token = token,
            GameId = gameId,
            PlayerNumber = int.Parse(playerNumberStr),
            PlayerName = playerName,
            ExpiresAt = fields.TryGetValue("expires_at", out var expiresAt)
                ? DateTime.Parse(expiresAt)
                : DateTime.UtcNow
        };
    }

    public async Task RemoveReconnectTokenAsync(string token)
    {
        var tokenKey = ReconnectTokenKeyPrefix + token;
        var fields = await _redis.HashGetAllAsync(tokenKey);

        if (fields.TryGetValue("game_id", out var gameId))
        {
            var gameTokensKey = GameTokensKeyPrefix + gameId;
            await _redis.SetRemoveAsync(gameTokensKey, token);
        }

        await _redis.KeyDeleteAsync(tokenKey);
        _logger.LogDebug("Removed reconnect token {Token}", token);
    }

    public async Task RemoveGameTokensAsync(string gameId)
    {
        var gameTokensKey = GameTokensKeyPrefix + gameId;
        var tokens = await _redis.SetMembersAsync(gameTokensKey);

        foreach (var token in tokens)
        {
            var tokenKey = ReconnectTokenKeyPrefix + token;
            await _redis.KeyDeleteAsync(tokenKey);
        }

        await _redis.KeyDeleteAsync(gameTokensKey);
        _logger.LogDebug("Removed all reconnect tokens for game {GameId}", gameId);
    }

    // Player connection state methods
    public async Task SetPlayerConnectedAsync(string gameId, int playerNumber, bool connected, string? connectionId)
    {
        var gameKey = GameKeyPrefix + gameId;
        var fields = new Dictionary<string, string>
        {
            [$"player{playerNumber}_connected"] = connected.ToString().ToLower()
        };

        if (connectionId != null)
        {
            fields[$"player{playerNumber}_id"] = connectionId;
        }

        await _redis.HashSetAsync(gameKey, fields);
        _logger.LogDebug("Set player {PlayerNumber} connected={Connected} for game {GameId}", playerNumber, connected, gameId);
    }

    public async Task<PlayerConnectionState> GetPlayersConnectionStateAsync(string gameId)
    {
        var gameKey = GameKeyPrefix + gameId;
        var fields = await _redis.HashGetAllAsync(gameKey);

        return new PlayerConnectionState
        {
            Player1Connected = fields.TryGetValue("player1_connected", out var p1c) && p1c == "true",
            Player2Connected = fields.TryGetValue("player2_connected", out var p2c) && p2c == "true",
            Player1ConnectionId = fields.TryGetValue("player1_id", out var p1Id) ? p1Id : null,
            Player2ConnectionId = fields.TryGetValue("player2_id", out var p2Id) ? p2Id : null
        };
    }

    public async Task UpdatePlayerConnectionIdAsync(string gameId, int playerNumber, string newConnectionId)
    {
        var gameKey = GameKeyPrefix + gameId;
        await _redis.HashSetAsync(gameKey, $"player{playerNumber}_id", newConnectionId);
        _logger.LogDebug("Updated connection ID for player {PlayerNumber} in game {GameId} to {ConnectionId}", playerNumber, gameId, newConnectionId);
    }

    // Paused games methods
    public async Task MoveToPausedGamesAsync(string gameId)
    {
        await _redis.SetRemoveAsync(PlayingGamesKey, gameId);
        await _redis.SetAddAsync(PausedGamesKey, gameId);

        var gameKey = GameKeyPrefix + gameId;
        await _redis.HashSetAsync(gameKey, "state", GameState.Paused.ToString());

        _logger.LogDebug("Moved game {GameId} to paused games", gameId);
    }

    public async Task MoveFromPausedToPlayingAsync(string gameId)
    {
        await _redis.SetRemoveAsync(PausedGamesKey, gameId);
        await _redis.SetAddAsync(PlayingGamesKey, gameId);

        var gameKey = GameKeyPrefix + gameId;
        await _redis.HashSetAsync(gameKey, "state", GameState.Playing.ToString());

        _logger.LogDebug("Moved game {GameId} from paused to playing", gameId);
    }

    public async Task<List<string>> GetPausedGameIdsAsync()
    {
        return await _redis.SetMembersAsync(PausedGamesKey);
    }

    private static Dictionary<string, string> SerializeGame(Game game)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = game.Id,
            ["state"] = game.State.ToString(),
            ["ball_x"] = game.Ball.X.ToString(CultureInfo.InvariantCulture),
            ["ball_y"] = game.Ball.Y.ToString(CultureInfo.InvariantCulture),
            ["ball_vx"] = game.Ball.VelocityX.ToString(CultureInfo.InvariantCulture),
            ["ball_vy"] = game.Ball.VelocityY.ToString(CultureInfo.InvariantCulture),
            ["paddle1_y"] = game.Paddle1.Y.ToString(CultureInfo.InvariantCulture),
            ["paddle2_y"] = game.Paddle2.Y.ToString(CultureInfo.InvariantCulture)
        };

        if (game.Player1 != null)
        {
            fields["player1_id"] = game.Player1.ConnectionId;
            fields["player1_name"] = game.Player1.Name;
            fields["player1_score"] = game.Player1.Score.ToString();
        }

        if (game.Player2 != null)
        {
            fields["player2_id"] = game.Player2.ConnectionId;
            fields["player2_name"] = game.Player2.Name;
            fields["player2_score"] = game.Player2.Score.ToString();
        }

        return fields;
    }

    private static Game? DeserializeGame(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("id", out var id))
            return null;

        var game = new Game { Id = id };

        if (fields.TryGetValue("state", out var stateStr) &&
            Enum.TryParse<GameState>(stateStr, out var state))
        {
            game.State = state;
        }

        if (fields.TryGetValue("ball_x", out var ballX))
            game.Ball.X = double.Parse(ballX, CultureInfo.InvariantCulture);
        if (fields.TryGetValue("ball_y", out var ballY))
            game.Ball.Y = double.Parse(ballY, CultureInfo.InvariantCulture);
        if (fields.TryGetValue("ball_vx", out var ballVx))
            game.Ball.VelocityX = double.Parse(ballVx, CultureInfo.InvariantCulture);
        if (fields.TryGetValue("ball_vy", out var ballVy))
            game.Ball.VelocityY = double.Parse(ballVy, CultureInfo.InvariantCulture);

        if (fields.TryGetValue("paddle1_y", out var paddle1Y))
            game.Paddle1.Y = double.Parse(paddle1Y, CultureInfo.InvariantCulture);
        if (fields.TryGetValue("paddle2_y", out var paddle2Y))
            game.Paddle2.Y = double.Parse(paddle2Y, CultureInfo.InvariantCulture);

        if (fields.TryGetValue("player1_id", out var p1Id) &&
            fields.TryGetValue("player1_name", out var p1Name))
        {
            game.Player1 = new Player
            {
                ConnectionId = p1Id,
                Name = p1Name,
                Score = fields.TryGetValue("player1_score", out var p1Score)
                    ? int.Parse(p1Score)
                    : 0
            };
        }

        if (fields.TryGetValue("player2_id", out var p2Id) &&
            fields.TryGetValue("player2_name", out var p2Name))
        {
            game.Player2 = new Player
            {
                ConnectionId = p2Id,
                Name = p2Name,
                Score = fields.TryGetValue("player2_score", out var p2Score)
                    ? int.Parse(p2Score)
                    : 0
            };
        }

        return game;
    }
}
