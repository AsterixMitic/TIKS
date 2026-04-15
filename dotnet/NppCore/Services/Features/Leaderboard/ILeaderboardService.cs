using NppCore.Models;

namespace NppCore.Services.Features.Leaderboard;

public interface ILeaderboardService
{
    /// <summary>
    /// Returns global leaderboard for a specific period
    /// </summary>
    /// <param name="periodType">Period type: MONTHLY, YEARLY, ALL_TIME</param>
    /// <param name="periodId">Period ID: e.g. '2024-01', '2024', 'all'</param>
    /// <param name="limit">Number of results (default 10)</param>
    Task<GlobalLeaderboardResponse> GetGlobalLeaderboardAsync(string periodType, string periodId, int limit = 10);

    /// <summary>
    /// Adds or updates an entry in global leaderboard
    /// </summary>
    Task AddOrUpdateGlobalLeaderboardAsync(string periodType, string periodId, Guid playerId, string username, int rankScore);

    /// <summary>
    /// Returns leaderboard by number of wins
    /// </summary>
    /// <param name="category">Category (default 'most_wins')</param>
    /// <param name="limit">Number of results (default 10)</param>
    Task<WinsLeaderboardResponse> GetWinsLeaderboardAsync(string category = "most_wins", int limit = 10);

    /// <summary>
    /// Adds or updates an entry in wins leaderboard
    /// </summary>
    Task AddOrUpdateWinsLeaderboardAsync(string category, Guid playerId, string username, int gamesWon);

    /// <summary>
    /// Returns streak information for a player
    /// </summary>
    Task<PlayerStreakDto?> GetPlayerStreakAsync(Guid playerId);

    /// <summary>
    /// Updates streak information after a match
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <param name="username">Player username</param>
    /// <param name="won">Whether the player won</param>
    Task UpdatePlayerStreakAsync(Guid playerId, string username, bool won);

    /// <summary>
    /// Returns leaderboard by longest streak
    /// </summary>
    /// <param name="category">Category (default 'global_all_time')</param>
    /// <param name="limit">Number of results (default 10)</param>
    Task<StreakLeaderboardResponse> GetStreakLeaderboardAsync(string category = "global_all_time", int limit = 10);

    /// <summary>
    /// Adds or updates an entry in streak leaderboard
    /// </summary>
    Task AddOrUpdateStreakLeaderboardAsync(string category, Guid playerId, string username, int longestStreak);
}
