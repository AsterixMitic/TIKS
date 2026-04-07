using NppCore.Models;
using NppCore.Services.Persistence.Cassandra;

namespace NppCore.Services.Features.Player;

public class PlayerService : IPlayerService
{
    private readonly ICassandraService _cassandra;

    public PlayerService(ICassandraService cassandra)
    {
        _cassandra = cassandra;
    }

    public async Task<PlayerEntity> CreateAsync(string username, string email, string? avatarUrl = null)
    {
        var player = new PlayerEntity
        {
            PlayerId = Guid.NewGuid(),
            Username = username,
            Email = email,
            AvatarUrl = avatarUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _cassandra.ExecuteAsync(
            "INSERT INTO players (player_id, username, email, avatar_url, created_at) VALUES (?, ?, ?, ?, ?)",
            player.PlayerId,
            player.Username,
            player.Email,
            player.AvatarUrl!,
            player.CreatedAt
        );

        return player;
    }

    public async Task<PlayerEntity?> GetByIdAsync(Guid playerId)
    {
        return await _cassandra.QueryFirstOrDefaultAsync<PlayerEntity>(
            "SELECT player_id, username, email, avatar_url, created_at FROM players WHERE player_id = ?",
            playerId
        );
    }

    public async Task<PlayerEntity?> GetByEmailAsync(string email)
    {
        var playerByEmail = await _cassandra.QueryFirstOrDefaultAsync<PlayerByEmail>(
            "SELECT email, password_hash, player_id FROM players_by_email WHERE email = ?",
            email
        );

        if (playerByEmail == null)
            return null;

        return await GetByIdAsync(playerByEmail.PlayerId);
    }

    public async Task<PlayerEntity?> GetByUsernameAsync(string username)
    {
        var playerByUsername = await _cassandra.QueryFirstOrDefaultAsync<PlayerByUsername>(
            "SELECT username, player_id FROM players_by_username WHERE username = ?",
            username
        );

        if (playerByUsername == null)
            return null;

        return await GetByIdAsync(playerByUsername.PlayerId);
    }

    public async Task<PlayerEntity?> UpdateAsync(Guid playerId, string? username, string? avatarUrl)
    {
        var player = await GetByIdAsync(playerId);
        if (player == null) return null;

        var newUsername = username ?? player.Username;
        var newAvatarUrl = avatarUrl ?? player.AvatarUrl;

        if (username != null && username != player.Username)
        {
            await _cassandra.ExecuteAsync(
                "DELETE FROM players_by_username WHERE username = ?",
                player.Username
            );
            await _cassandra.ExecuteAsync(
                "INSERT INTO players_by_username (username, player_id) VALUES (?, ?)",
                newUsername, playerId
            );
        }

        await _cassandra.ExecuteAsync(
            "UPDATE players SET username = ?, avatar_url = ? WHERE player_id = ?",
            newUsername, newAvatarUrl!, playerId
        );

        player.Username = newUsername;
        player.AvatarUrl = newAvatarUrl;
        return player;
    }

    public async Task<bool> DeleteAsync(Guid playerId)
    {
        var player = await GetByIdAsync(playerId);
        if (player == null) return false;

        await _cassandra.ExecuteAsync(
            "DELETE FROM players_by_username WHERE username = ?",
            player.Username
        );
        await _cassandra.ExecuteAsync(
            "DELETE FROM players_by_email WHERE email = ?",
            player.Email
        );
        await _cassandra.ExecuteAsync(
            "DELETE FROM players WHERE player_id = ?",
            playerId
        );

        return true;
    }
}
