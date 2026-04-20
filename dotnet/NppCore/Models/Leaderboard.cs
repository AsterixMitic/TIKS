namespace NppCore.Models;

public class GlobalLeaderboardEntry
{
    public string PeriodType { get; set; } = string.Empty; // 'MONTHLY', 'YEARLY', 'ALL_TIME'
    public string PeriodId { get; set; } = string.Empty; // e.g. '2024-01'
    public int RankScore { get; set; }
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class WinsLeaderboardEntry
{
    public string Category { get; set; } = string.Empty; // e.g. 'most_wins'
    public int GamesWon { get; set; }
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class PlayerStreak
{
    public Guid PlayerId { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public string LastResult { get; set; } = string.Empty; // 'WIN' or 'LOSS'
}

public class StreakLeaderboardEntry
{
    public string Category { get; set; } = string.Empty; // e.g. 'global_all_time'
    public int LongestStreak { get; set; }
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class GlobalLeaderboardByPlayerEntry
{
    public string PeriodType { get; set; } = string.Empty;
    public string PeriodId { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int RankScore { get; set; }
}

public class WinsLeaderboardByPlayerEntry
{
    public string Category { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int GamesWon { get; set; }
}

public class StreakLeaderboardByPlayerEntry
{
    public string Category { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int LongestStreak { get; set; }
}

public record LeaderboardEntryDto(
    int Rank,
    Guid PlayerId,
    string Username,
    int Score
);

public record PlayerStreakDto(
    Guid PlayerId,
    int CurrentStreak,
    int LongestStreak,
    string LastResult
);

public record GlobalLeaderboardResponse(
    string PeriodType,
    string PeriodId,
    List<LeaderboardEntryDto> Entries
);

public record WinsLeaderboardResponse(
    string Category,
    List<LeaderboardEntryDto> Entries
);

public record StreakLeaderboardResponse(
    string Category,
    List<LeaderboardEntryDto> Entries
);
