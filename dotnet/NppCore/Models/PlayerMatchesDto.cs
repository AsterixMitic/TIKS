namespace NppCore.Models;

public record PlayerMatchesRequest(
    Guid OpponentId, 
    string OpponentUsername, 
    string Score, 
    string Result, 
    string Year, 
    DateTimeOffset Match_time
);

public record PlayerMatchesResponse
(
    Guid PlayerId,
    Guid MatchId,
    string OpponentUsername,
    string Result,
    string Score,
    DateTimeOffset Match_time
);

public record MatchHistoryRequest 
(

);
public record MatchHistoryResponse
(
    String P1Username,
    String P2Username,
    String Score,
    String Result

);