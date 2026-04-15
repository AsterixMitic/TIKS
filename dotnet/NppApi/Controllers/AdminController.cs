using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NppCore.Constants;
using NppCore.Models;
using NppCore.Services.Features.Leaderboard;
using NppCore.Services.Persistence.Cassandra;

namespace NppApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ICassandraService _cassandra;
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ICassandraService cassandra,
        ILeaderboardService leaderboardService,
        ILogger<AdminController> logger)
    {
        _cassandra = cassandra;
        _leaderboardService = leaderboardService;
        _logger = logger;
    }

    /// <summary>
    /// Manually trigger leaderboard snapshot
    /// POST /api/admin/snapshot-leaderboards
    /// </summary>
    [HttpPost("snapshot-leaderboards")]
    public async Task<ActionResult> SnapshotLeaderboards()
    {
        try
        {
            _logger.LogInformation("Manual leaderboard snapshot triggered");

            var now = DateTimeOffset.UtcNow;
            var currentMonth = $"{now.Year}-{now.Month:D2}";
            var currentYear = $"{now.Year}";

            // Load all player_stats
            var playerStatsQuery = @"
                SELECT player_id, total_points, games_won, games_lost
                FROM player_stats";

            var playerStats = await _cassandra.QueryAsync<PlayerStatsSnapshot>(playerStatsQuery);
            var statsList = playerStats.ToList();

            int updatedCount = 0;

            foreach (var stats in statsList)
            {
                var player = await _cassandra.QueryFirstOrDefaultAsync<PlayerInfo>(
                    "SELECT player_id, username FROM players WHERE player_id = ?",
                    stats.PlayerId
                );

                if (player == null) continue;

                // Wins Leaderboard
                if (stats.GamesWon > 0)
                {
                    await _leaderboardService.AddOrUpdateWinsLeaderboardAsync(
                        GameConstants.LeaderboardCategoryMostWins,
                        stats.PlayerId,
                        player.Username,
                        (int)stats.GamesWon
                    );
                }

                // Global Leaderboard
                if (stats.TotalPoints > 0)
                {
                    await _leaderboardService.AddOrUpdateGlobalLeaderboardAsync(GameConstants.LeaderboardPeriodTypeMonthly, currentMonth, stats.PlayerId, player.Username, (int)stats.TotalPoints);
                    await _leaderboardService.AddOrUpdateGlobalLeaderboardAsync(GameConstants.LeaderboardPeriodTypeYearly, currentYear, stats.PlayerId, player.Username, (int)stats.TotalPoints);
                    await _leaderboardService.AddOrUpdateGlobalLeaderboardAsync(GameConstants.LeaderboardPeriodTypeAllTime, GameConstants.LeaderboardPeriodAllTime, stats.PlayerId, player.Username, (int)stats.TotalPoints);
                }

                updatedCount++;
            }

            return Ok(new
            {
                message = "Leaderboard snapshot completed",
                playersProcessed = updatedCount,
                timestamp = now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual leaderboard snapshot");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add points to a player in player_stats
    /// POST /api/admin/add-points
    /// </summary>
    [HttpPost("add-points")]
    public async Task<ActionResult> AddPoints([FromBody] AddPointsRequest request)
    {
        try
        {
            // Update counter table (counter columns require long values)
            await _cassandra.ExecuteAsync(
                @"UPDATE player_stats
                  SET total_points = total_points + ?,
                      games_won = games_won + ?,
                      games_lost = games_lost + ?
                  WHERE player_id = ?",
                (long)request.Points,
                (long)request.Wins,
                (long)request.Losses,
                request.PlayerId
            );

            return Ok(new
            {
                message = $"Added {request.Points} points, {request.Wins} wins, {request.Losses} losses to player {request.PlayerId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding points");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public record AddPointsRequest(
    [Required]
    Guid PlayerId,

    [Range(0, GameConstants.MaxPointsPerUpdate, ErrorMessage = "Points must be between 0 and 10000")]
    int Points,

    [Range(0, GameConstants.MaxWinsLossesPerUpdate, ErrorMessage = "Wins must be between 0 and 100")]
    int Wins,

    [Range(0, GameConstants.MaxWinsLossesPerUpdate, ErrorMessage = "Losses must be between 0 and 100")]
    int Losses
);
