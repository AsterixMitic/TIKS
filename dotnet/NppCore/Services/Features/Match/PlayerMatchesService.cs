using NppCore.Models;
using NppCore.Services.Persistence.Cassandra;

namespace NppCore.Services.Features.Match;

public class PlayerMatchesService : IPlayerMatchesService
{
    private readonly ICassandraService _cassandra;

    public PlayerMatchesService(ICassandraService cassandra)
    {
        _cassandra = cassandra;
    }

    public async Task<PlayerMatches> CreateAsync(Guid playerId, Guid opponentId, string year, DateTimeOffset mathc_time,Guid matchId ,string opponentUsername, string score, string result)
    {
        var playerMatches = new PlayerMatches
        {
            PlayerId = playerId,
            OpponentId = opponentId,
            Year = year,
            Result = result,
            Score = score,
            Match_time = mathc_time,
            OpponentUsername = opponentUsername,
            MatchId=matchId,
        };

        await _cassandra.ExecuteAsync(
            "INSERT INTO player_matches (player_id,year,match_time,match_id,opponent_id,opponent_username,result,score) VALUES (?, ?, ?, ?, ?,?,?,?)",
            playerMatches.PlayerId,
            playerMatches.Year,
            playerMatches.Match_time,
            playerMatches.MatchId,
            playerMatches.OpponentId,
            playerMatches.OpponentUsername,
            playerMatches.Result,
            playerMatches.Score

        );

        return playerMatches;
    }

    public async Task<IEnumerable<PlayerMatches>> GetByYearAsync(string year, Guid playerId, int page, int limit)
    {
        var query = "SELECT * FROM player_matches WHERE year = ? AND player_id = ?";
        var result = await _cassandra.QueryAsync<PlayerMatches>(query, year, playerId);

        var pagedResult = result.Skip((page - 1) * limit).Take(limit).ToList();
        return pagedResult;
    }

    public async Task<MatchHistory> CreateHistoryAsync(DateTimeOffset mt, String p1, String p2, String score, string result,Guid matchId)
    {
        string bucket=mt.ToString("yyyy-MM");
        
        var query = "INSERT INTO recent_matches (period,match_time,match_id,player1_username,player2_username,score,result) VALUES (?,?,?,?,?,?,?)";
        var match = new MatchHistory
        {
            Period = bucket,
            Match_time = mt,
            MatchId = matchId,
            player1Username = p1,
            player2Username = p2,
            Score = score,
            Result = result
        };
        await _cassandra.ExecuteAsync(query, match.Period, match.Match_time, match.MatchId, match.player1Username, match.player2Username, match.Score, match.Result);

        return match;

    }

    public async Task<IEnumerable<MatchHistory>> GetHistoryAsync(String period, int page, int limit)
    {
        var query = "SELECT * FROM recent_matches WHERE period=?";
        var res = await _cassandra.QueryAsync<MatchHistory>(query, period);
        var result = res.Skip((page - 1) * limit)
                        .Take(limit).ToList();

        return result;
    }

    public async Task<bool> DeleteAsync(Guid playerId, string year, Guid matchId)
    {
        // match_time is a required clustering key that precedes match_id in the primary key,
        // so we must retrieve it first before we can issue a DELETE.
        var existing = await _cassandra.QueryFirstOrDefaultAsync<PlayerMatches>(
            "SELECT match_time FROM player_matches WHERE player_id = ? AND year = ? AND match_id = ? ALLOW FILTERING",
            playerId, year, matchId
        );

        if (existing == null) return false;

        await _cassandra.ExecuteAsync(
            "DELETE FROM player_matches WHERE player_id = ? AND year = ? AND match_time = ? AND match_id = ?",
            playerId, year, existing.Match_time, matchId
        );

        return true;
    }
}