using NppCore.Models;

namespace NppCore.Services.Features.Player;

public interface IPlayerStatsService
{
    /// <summary>
    /// Gets player stats by player ID
    /// </summary>
    Task<PlayerStatsSnapshot?> GetStatsAsync(Guid playerId);

    /// <summary>
    /// Updates stats after a match
    /// </summary>
    Task UpdateStatsAfterMatchAsync(
        Guid winnerId,
        string winnerUsername,
        int winnerScore,
        Guid loserId,
        string loserUsername,
        int loserScore,
        Guid matchId,
        DateTimeOffset matchTime
    );
}
