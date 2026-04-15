using NppCore.Constants;
using NppCore.Models;
using NppCore.Services.Features.Leaderboard;
using NppCore.Services.Persistence.Cassandra;

namespace NppApi.Services;

public class LeaderboardSnapshotService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<LeaderboardSnapshotService> _logger;

    public LeaderboardSnapshotService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<LeaderboardSnapshotService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LeaderboardSnapshotService started");

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        if (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Running initial leaderboard snapshot on startup...");
            try
            {
                await UpdateLeaderboardsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in initial leaderboard snapshot");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(0);
                var delay = nextRun - now;

                _logger.LogInformation($"Next leaderboard snapshot will run at {nextRun:yyyy-MM-dd HH:mm:ss} UTC (in {delay.TotalHours:F2} hours)");

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await UpdateLeaderboardsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LeaderboardSnapshotService");
                // Wait 1 hour before retrying
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task UpdateLeaderboardsAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var cassandra = scope.ServiceProvider.GetRequiredService<ICassandraService>();
        var leaderboardService = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();

        try
        {
            _logger.LogInformation("Starting leaderboard snapshot update...");

            var now = DateTimeOffset.UtcNow;
            var currentMonth = $"{now.Year}-{now.Month:D2}";
            var currentYear = $"{now.Year}";

            // 1. Load all player_stats
            var playerStatsQuery = @"
                SELECT player_id, total_points, games_won, games_lost
                FROM player_stats";

            var playerStats = await cassandra.QueryAsync<PlayerStatsSnapshot>(playerStatsQuery);
            var statsList = playerStats.ToList();

            _logger.LogInformation($"Found {statsList.Count} players in player_stats");

            // 2. For each player, load username from players table
            foreach (var stats in statsList)
            {
                try
                {
                    var player = await cassandra.QueryFirstOrDefaultAsync<PlayerInfo>(
                        "SELECT player_id, username FROM players WHERE player_id = ?",
                        stats.PlayerId
                    );

                    if (player == null)
                    {
                        _logger.LogWarning($"Player {stats.PlayerId} not found in players table");
                        continue;
                    }

                    // 3. Update Wins Leaderboard
                    if (stats.GamesWon > 0)
                    {
                        await leaderboardService.AddOrUpdateWinsLeaderboardAsync(
                            GameConstants.LeaderboardCategoryMostWins,
                            stats.PlayerId,
                            player.Username,
                            (int)stats.GamesWon
                        );
                    }

                    // 4. Update Global Leaderboard
                    var monthlyPoints = await GetMonthlyPointsAsync(cassandra, currentMonth, stats.PlayerId);
                    if (monthlyPoints > 0)
                    {
                        await leaderboardService.AddOrUpdateGlobalLeaderboardAsync(
                            GameConstants.LeaderboardPeriodTypeMonthly,
                            currentMonth,
                            stats.PlayerId,
                            player.Username,
                            (int)monthlyPoints
                        );
                    }

                    var yearlyPoints = await GetYearlyPointsAsync(cassandra, currentYear, stats.PlayerId);
                    if (yearlyPoints > 0)
                    {
                        await leaderboardService.AddOrUpdateGlobalLeaderboardAsync(
                            GameConstants.LeaderboardPeriodTypeYearly,
                            currentYear,
                            stats.PlayerId,
                            player.Username,
                            (int)yearlyPoints
                        );
                    }

                    if (stats.TotalPoints > 0)
                    {
                        // All-time
                        await leaderboardService.AddOrUpdateGlobalLeaderboardAsync(
                            GameConstants.LeaderboardPeriodTypeAllTime,
                            GameConstants.LeaderboardPeriodAllTime,
                            stats.PlayerId,
                            player.Username,
                            (int)stats.TotalPoints
                        );
                    }

                    _logger.LogInformation($"Updated leaderboards for player {player.Username} (Points: {stats.TotalPoints}, Wins: {stats.GamesWon})");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error updating leaderboards for player {stats.PlayerId}");
                }
            }

            _logger.LogInformation("Leaderboard snapshot completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating leaderboards");
            throw;
        }
    }

    private class PointsCounterRow
    {
        public long TotalPoints { get; set; }
    }

    private static async Task<long> GetMonthlyPointsAsync(ICassandraService cassandra, string yearMonth, Guid playerId)
    {
        var row = await cassandra.QueryFirstOrDefaultAsync<PointsCounterRow>(
            "SELECT total_points FROM monthly_leaderboard WHERE year_month = ? AND player_id = ?",
            yearMonth, playerId
        );

        return row?.TotalPoints ?? 0;
    }

    private static async Task<long> GetYearlyPointsAsync(ICassandraService cassandra, string year, Guid playerId)
    {
        var row = await cassandra.QueryFirstOrDefaultAsync<PointsCounterRow>(
            "SELECT total_points FROM yearly_leaderboard WHERE year = ? AND player_id = ?",
            year, playerId
        );

        return row?.TotalPoints ?? 0;
    }
}
