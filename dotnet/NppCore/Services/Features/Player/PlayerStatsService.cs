using Microsoft.Extensions.Logging;
using NppCore.Constants;
using NppCore.Services.Features.Leaderboard;
using NppCore.Services.Persistence.Cassandra;

namespace NppCore.Services.Features.Player;

public class PlayerStatsService : IPlayerStatsService
{
    private readonly ICassandraService _cassandra;
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<PlayerStatsService> _logger;

    public PlayerStatsService(
        ICassandraService cassandra, 
        ILeaderboardService leaderboardService,
        ILogger<PlayerStatsService> logger)
    {
        _cassandra = cassandra;
        _leaderboardService = leaderboardService;
        _logger = logger;
    }

    public async Task<Models.PlayerStatsSnapshot?> GetStatsAsync(Guid playerId)
    {
        return await _cassandra.QueryFirstOrDefaultAsync<Models.PlayerStatsSnapshot>(
            "SELECT player_id, total_points, games_won, games_lost FROM player_stats WHERE player_id = ?",
            playerId
        );
    }

    public async Task UpdateStatsAfterMatchAsync(
        Guid winnerId,
        string winnerUsername,
        int winnerScore,
        Guid loserId,
        string loserUsername,
        int loserScore,
        Guid matchId,
        DateTimeOffset matchTime)
    {
        var now = matchTime;
        var currentMonth = $"{now.Year}-{now.Month:D2}";
        var currentYear = $"{now.Year}";
        long winnerPoints = winnerScore * GameConstants.PointsPerScore;
        long loserPoints = loserScore * GameConstants.PointsPerScore;

        // DUAL WRITE PROBLEM MITIGATION:
        // Cassandra doesn't have ACID transactions across multiple tables.
        // Log errors and continue with remaining operations.
        
        try
        {
            // 1. Update player_stats counter table (CRITICAL - must succeed)
            await _cassandra.ExecuteAsync(
                "UPDATE player_stats SET total_points = total_points + ?, games_won = games_won + 1 WHERE player_id = ?",
                winnerPoints, winnerId
            );

            await _cassandra.ExecuteAsync(
                "UPDATE player_stats SET total_points = total_points + ?, games_lost = games_lost + 1 WHERE player_id = ?",
                loserPoints, loserId
            );

            // Update monthly and yearly points counters
            await _cassandra.ExecuteAsync(
                "UPDATE monthly_leaderboard SET total_points = total_points + ? WHERE year_month = ? AND player_id = ?",
                winnerPoints, currentMonth, winnerId
            );

            await _cassandra.ExecuteAsync(
                "UPDATE monthly_leaderboard SET total_points = total_points + ? WHERE year_month = ? AND player_id = ?",
                loserPoints, currentMonth, loserId
            );

            await _cassandra.ExecuteAsync(
                "UPDATE yearly_leaderboard SET total_points = total_points + ? WHERE year = ? AND player_id = ?",
                winnerPoints, currentYear, winnerId
            );

            await _cassandra.ExecuteAsync(
                "UPDATE yearly_leaderboard SET total_points = total_points + ? WHERE year = ? AND player_id = ?",
                loserPoints, currentYear, loserId
            );
            
            _logger.LogInformation("Updated player_stats for winner {WinnerId} and loser {LoserId}", winnerId, loserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL: Failed to update player_stats for match {Winner} vs {Loser}", winnerId, loserId);
            throw; // Re-throw because this is critical
        }

        try
        {
            // 2. Save match history (IMPORTANT - but not critical)
            var year = now.Year.ToString();
            var matchScore = $"{winnerScore}:{loserScore}";

            await _cassandra.ExecuteAsync(
                @"INSERT INTO player_matches (player_id, year, match_time, match_id, opponent_id, opponent_username, score, result) 
                  VALUES (?, ?, ?, ?, ?, ?, ?, 'WIN')",
                winnerId, year, now, matchId, loserId, loserUsername, matchScore
            );

            await _cassandra.ExecuteAsync(
                @"INSERT INTO player_matches (player_id, year, match_time, match_id, opponent_id, opponent_username, score, result) 
                  VALUES (?, ?, ?, ?, ?, ?, ?, 'LOSS')",
                loserId, year, now, matchId, winnerId, winnerUsername, matchScore
            );
            
            _logger.LogInformation("Saved match history for {Winner} vs {Loser}", winnerId, loserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save match history for {Winner} vs {Loser}", winnerId, loserId);
            // Don't throw, continue with other operations
        }

        try
        {
            // 3. Update streaks (IMPORTANT)
            await _leaderboardService.UpdatePlayerStreakAsync(winnerId, winnerUsername, won: true);
            await _leaderboardService.UpdatePlayerStreakAsync(loserId, loserUsername, won: false);
            
            _logger.LogInformation("Updated streaks for {Winner} and {Loser}", winnerId, loserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update streaks for {Winner} and {Loser}", winnerId, loserId);
        }

        try
        {
            // 4. Update leaderboards (IMPORTANT)
            await UpdatePlayerLeaderboardsAsync(winnerId, winnerUsername, currentMonth, currentYear, includeWins: true);
            await UpdatePlayerLeaderboardsAsync(loserId, loserUsername, currentMonth, currentYear, includeWins: false);
            
            _logger.LogInformation("Updated leaderboards for {Winner} and {Loser}", winnerId, loserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update leaderboards for {Winner} and {Loser}", winnerId, loserId);
        }
    }

    /// <summary>
    /// Helper method that eliminates code duplication for leaderboard updates
    /// </summary>
    private async Task UpdatePlayerLeaderboardsAsync(
        Guid playerId, 
        string username, 
        string currentMonth, 
        string currentYear,
        bool includeWins)
    {
        var playerStats = await _cassandra.QueryFirstOrDefaultAsync<Models.PlayerStatsSnapshot>(
            "SELECT player_id, total_points, games_won, games_lost FROM player_stats WHERE player_id = ?",
            playerId
        );

        if (playerStats == null) return;

        // Wins leaderboard (only for winners)
        if (includeWins && playerStats.GamesWon > 0)
        {
            await _leaderboardService.AddOrUpdateWinsLeaderboardAsync(
                GameConstants.LeaderboardCategoryMostWins, 
                playerId, 
                username, 
                (int)playerStats.GamesWon
            );
        }

        var monthlyPoints = await GetMonthlyPointsAsync(currentMonth, playerId);
        if (monthlyPoints > 0)
        {
            await _leaderboardService.AddOrUpdateGlobalLeaderboardAsync(
                GameConstants.LeaderboardPeriodTypeMonthly, currentMonth, playerId, username, (int)monthlyPoints);
        }

        var yearlyPoints = await GetYearlyPointsAsync(currentYear, playerId);
        if (yearlyPoints > 0)
        {
            await _leaderboardService.AddOrUpdateGlobalLeaderboardAsync(
                GameConstants.LeaderboardPeriodTypeYearly, currentYear, playerId, username, (int)yearlyPoints);
        }

        // All-time leaderboard (from player_stats)
        if (playerStats.TotalPoints > 0)
        {
            await _leaderboardService.AddOrUpdateGlobalLeaderboardAsync(
                GameConstants.LeaderboardPeriodTypeAllTime, GameConstants.LeaderboardPeriodAllTime, playerId, username, (int)playerStats.TotalPoints);
        }
    }

    private class PointsCounterRow
    {
        public long TotalPoints { get; set; }
    }

    private async Task<long> GetMonthlyPointsAsync(string yearMonth, Guid playerId)
    {
        var row = await _cassandra.QueryFirstOrDefaultAsync<PointsCounterRow>(
            "SELECT total_points FROM monthly_leaderboard WHERE year_month = ? AND player_id = ?",
            yearMonth, playerId
        );

        return row?.TotalPoints ?? 0;
    }

    private async Task<long> GetYearlyPointsAsync(string year, Guid playerId)
    {
        var row = await _cassandra.QueryFirstOrDefaultAsync<PointsCounterRow>(
            "SELECT total_points FROM yearly_leaderboard WHERE year = ? AND player_id = ?",
            year, playerId
        );

        return row?.TotalPoints ?? 0;
    }
}
