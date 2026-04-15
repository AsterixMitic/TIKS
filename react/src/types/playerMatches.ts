export interface PlayerMatches
{
    playerId:string;
    opponentId:string;
    opponentUsername:string;
    score:string;
    result:string;
    year:string;
    match_time:string;
}

export interface PlayerMatchesRequest
{
    opponentId:string;
    opponentUsername:string;
    score:string;
    result:string;
    year:string;
    match_time:string;
}

export interface PlayerMatchesResponse
{
    playerId:string;
    matchId:string;
    opponentUsername:string;
    result:string;
    score:string;
    match_time:string;
}

export interface MatchHistory
{
    p1Username: string;
    p2Username: string;
    score: string;
    result: string;
}