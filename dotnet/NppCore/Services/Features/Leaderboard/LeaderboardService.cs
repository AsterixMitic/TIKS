using NppCore.Constants;
using NppCore.Models;
using NppCore.Services.Persistence.Cassandra;

namespace NppCore.Services.Features.Leaderboard;

public class LeaderboardService : ILeaderboardService
{
    private readonly ICassandraService _cassandra;

    public LeaderboardService(ICassandraService cassandra)
    {
        _cassandra = cassandra;
    }

    public async Task<GlobalLeaderboardResponse> GetGlobalLeaderboardAsync(string periodType, string periodId, int limit = 10)
    {
        var cql = @"
            SELECT period_type, period_id, rank_score, player_id, username 
            FROM global_leaderboard 
            WHERE period_type = ? AND period_id = ? 
            LIMIT ?";

        var entries = await _cassandra.QueryAsync<GlobalLeaderboardEntry>(cql, periodType, periodId, limit);
        
        var entriesList = entries.ToList();
        var dtos = entriesList.Select((entry, index) => new LeaderboardEntryDto(
            Rank: index + 1,
            PlayerId: entry.PlayerId,
            Username: entry.Username,
            Score: entry.RankScore
        )).ToList();

        return new GlobalLeaderboardResponse(periodType, periodId, dtos);
    }

    public async Task AddOrUpdateGlobalLeaderboardAsync(string periodType, string periodId, Guid playerId, string username, int rankScore)
    {
        var existing = await GetGlobalLeaderboardByPlayerAsync(periodType, periodId, playerId);

        if (existing != null && existing.RankScore != rankScore)
        {
            var cql = @"
                BEGIN BATCH
                DELETE FROM global_leaderboard
                WHERE period_type = ? AND period_id = ? AND rank_score = ? AND player_id = ?;

                INSERT INTO global_leaderboard (period_type, period_id, rank_score, player_id, username)
                VALUES (?, ?, ?, ?, ?);

                INSERT INTO global_leaderboard_by_player (period_type, period_id, player_id, username, rank_score)
                VALUES (?, ?, ?, ?, ?);
            APPLY BATCH;";

            await _cassandra.ExecuteAsync(
                cql,
                periodType, periodId, existing.RankScore, playerId,
                periodType, periodId, rankScore, playerId, username,
                periodType, periodId, playerId, username, rankScore
            );

            return;
        }

        var upsert = @"
            BEGIN BATCH
            INSERT INTO global_leaderboard (period_type, period_id, rank_score, player_id, username)
            VALUES (?, ?, ?, ?, ?);

            INSERT INTO global_leaderboard_by_player (period_type, period_id, player_id, username, rank_score)
            VALUES (?, ?, ?, ?, ?);
        APPLY BATCH;";

        await _cassandra.ExecuteAsync(
            upsert,
            periodType, periodId, rankScore, playerId, username,
            periodType, periodId, playerId, username, rankScore
        );
    }

    public async Task<WinsLeaderboardResponse> GetWinsLeaderboardAsync(string category = "most_wins", int limit = 10)
    {
        var cql = @"
            SELECT category, games_won, player_id, username 
            FROM leaderboard_by_wins 
            WHERE category = ? 
            LIMIT ?";

        var entries = await _cassandra.QueryAsync<WinsLeaderboardEntry>(cql, category, limit);
        
        var entriesList = entries.ToList();
        var dtos = entriesList.Select((entry, index) => new LeaderboardEntryDto(
            Rank: index + 1,
            PlayerId: entry.PlayerId,
            Username: entry.Username,
            Score: entry.GamesWon
        )).ToList();

        return new WinsLeaderboardResponse(category, dtos);
    }

    public async Task AddOrUpdateWinsLeaderboardAsync(string category, Guid playerId, string username, int gamesWon)
    {
        var existing = await GetWinsLeaderboardByPlayerAsync(category, playerId);

        if (existing != null && existing.GamesWon != gamesWon)
        {
            var cql = @"
                BEGIN BATCH
                DELETE FROM leaderboard_by_wins WHERE category = ? AND games_won = ? AND player_id = ?;

                INSERT INTO leaderboard_by_wins (category, games_won, player_id, username) 
                VALUES (?, ?, ?, ?);

                INSERT INTO leaderboard_by_wins_by_player (category, player_id, username, games_won)
                VALUES (?, ?, ?, ?);
            APPLY BATCH;";

            await _cassandra.ExecuteAsync(
                cql,
                category, existing.GamesWon, playerId,
                category, gamesWon, playerId, username,
                category, playerId, username, gamesWon
            );

            return;
        }

        var upsert = @"
            BEGIN BATCH
            INSERT INTO leaderboard_by_wins (category, games_won, player_id, username) 
            VALUES (?, ?, ?, ?);

            INSERT INTO leaderboard_by_wins_by_player (category, player_id, username, games_won)
            VALUES (?, ?, ?, ?);
        APPLY BATCH;";

        await _cassandra.ExecuteAsync(
            upsert,
            category, gamesWon, playerId, username,
            category, playerId, username, gamesWon
        );
    }

    public async Task<PlayerStreakDto?> GetPlayerStreakAsync(Guid playerId)
    {
        var cql = @"
            SELECT player_id, current_streak, longest_streak, last_result 
            FROM player_current_streak 
            WHERE player_id = ?";

        var streak = await _cassandra.QueryFirstOrDefaultAsync<PlayerStreak>(cql, playerId);

        if (streak == null)
            return null;

        return new PlayerStreakDto(
            streak.PlayerId,
            streak.CurrentStreak,
            streak.LongestStreak,
            streak.LastResult
        );
    }

    public async Task UpdatePlayerStreakAsync(Guid playerId, string username, bool won)
    {
        var currentStreak = await _cassandra.QueryFirstOrDefaultAsync<PlayerStreak>(
            "SELECT player_id, current_streak, longest_streak, last_result FROM player_current_streak WHERE player_id = ?",
            playerId
        );

        int newCurrentStreak;
        int newLongestStreak;
        string newLastResult = won ? GameConstants.ResultWin : GameConstants.ResultLoss;

        if (currentStreak == null)
        {
            newCurrentStreak = won ? 1 : 0;
            newLongestStreak = won ? 1 : 0;
        }
        else
        {
            if (won)
            {
                newCurrentStreak = currentStreak.CurrentStreak + 1;
                newLongestStreak = Math.Max(newCurrentStreak, currentStreak.LongestStreak);
            }
            else
            {
                newCurrentStreak = 0;
                newLongestStreak = currentStreak.LongestStreak;
            }
        }

        // INSERT will overwrite existing row since player_id is PRIMARY KEY
        var cql = @"
            INSERT INTO player_current_streak (player_id, current_streak, longest_streak, last_result) 
            VALUES (?, ?, ?, ?)";

        await _cassandra.ExecuteAsync(cql, playerId, newCurrentStreak, newLongestStreak, newLastResult);

        if (currentStreak == null || newLongestStreak > currentStreak.LongestStreak)
        {
            await AddOrUpdateStreakLeaderboardAsync(GameConstants.LeaderboardCategoryGlobalAllTime, playerId, username, newLongestStreak);
        }
    }

    public async Task<StreakLeaderboardResponse> GetStreakLeaderboardAsync(string category = "global_all_time", int limit = 10)
    {
        var cql = @"
            SELECT category, longest_streak, player_id, username 
            FROM leaderboard_by_longest_streak 
            WHERE category = ? 
            LIMIT ?";

        var entries = await _cassandra.QueryAsync<StreakLeaderboardEntry>(cql, category, limit);
        
        var entriesList = entries.ToList();
        var dtos = entriesList.Select((entry, index) => new LeaderboardEntryDto(
            Rank: index + 1,
            PlayerId: entry.PlayerId,
            Username: entry.Username,
            Score: entry.LongestStreak
        )).ToList();

        return new StreakLeaderboardResponse(category, dtos);
    }

    public async Task AddOrUpdateStreakLeaderboardAsync(string category, Guid playerId, string username, int longestStreak)
    {
        var existing = await GetStreakLeaderboardByPlayerAsync(category, playerId);

        if (existing != null && existing.LongestStreak != longestStreak)
        {
            var cql = @"
                BEGIN BATCH
                DELETE FROM leaderboard_by_longest_streak WHERE category = ? AND longest_streak = ? AND player_id = ?;

                INSERT INTO leaderboard_by_longest_streak (category, longest_streak, player_id, username) 
                VALUES (?, ?, ?, ?);

                INSERT INTO leaderboard_by_longest_streak_by_player (category, player_id, username, longest_streak)
                VALUES (?, ?, ?, ?);
            APPLY BATCH;";

            await _cassandra.ExecuteAsync(
                cql,
                category, existing.LongestStreak, playerId,
                category, longestStreak, playerId, username,
                category, playerId, username, longestStreak
            );

            return;
        }

        var upsert = @"
            BEGIN BATCH
            INSERT INTO leaderboard_by_longest_streak (category, longest_streak, player_id, username) 
            VALUES (?, ?, ?, ?);

            INSERT INTO leaderboard_by_longest_streak_by_player (category, player_id, username, longest_streak)
            VALUES (?, ?, ?, ?);
        APPLY BATCH;";

        await _cassandra.ExecuteAsync(
            upsert,
            category, longestStreak, playerId, username,
            category, playerId, username, longestStreak
        );
    }

    private Task<GlobalLeaderboardByPlayerEntry?> GetGlobalLeaderboardByPlayerAsync(string periodType, string periodId, Guid playerId)
    {
        var cql = @"
            SELECT period_type, period_id, player_id, username, rank_score
            FROM global_leaderboard_by_player
            WHERE period_type = ? AND period_id = ? AND player_id = ?";

        return _cassandra.QueryFirstOrDefaultAsync<GlobalLeaderboardByPlayerEntry>(cql, periodType, periodId, playerId);
    }

    private Task<WinsLeaderboardByPlayerEntry?> GetWinsLeaderboardByPlayerAsync(string category, Guid playerId)
    {
        var cql = @"
            SELECT category, player_id, username, games_won
            FROM leaderboard_by_wins_by_player
            WHERE category = ? AND player_id = ?";

        return _cassandra.QueryFirstOrDefaultAsync<WinsLeaderboardByPlayerEntry>(cql, category, playerId);
    }

    private Task<StreakLeaderboardByPlayerEntry?> GetStreakLeaderboardByPlayerAsync(string category, Guid playerId)
    {
        var cql = @"
            SELECT category, player_id, username, longest_streak
            FROM leaderboard_by_longest_streak_by_player
            WHERE category = ? AND player_id = ?";

        return _cassandra.QueryFirstOrDefaultAsync<StreakLeaderboardByPlayerEntry>(cql, category, playerId);
    }
}
