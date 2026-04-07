using NppCore.Models;

namespace NppCore.Services.Features.Match;

public interface IPlayerMatchesService
{
    Task<PlayerMatches> CreateAsync(Guid playerId,Guid opponentId, string year, DateTimeOffset mathc_time,Guid matchId ,string opponentUsername, string score,string result );
    Task<IEnumerable<PlayerMatches>> GetByYearAsync(string year,Guid playerId,int page,int limit);
    Task<IEnumerable<MatchHistory>> GetHistoryAsync(string bucket, int page=1,int limit=10);
    Task<MatchHistory> CreateHistoryAsync(DateTimeOffset mt, String p1, String p2, String score, string result ,Guid matchId);
    Task<bool> DeleteAsync(Guid playerId, string year, Guid matchId);
}