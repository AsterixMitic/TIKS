namespace NppCore.Models;

public class PlayerEntity
{
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class PlayerByEmail
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
}

// feature-mlacky
public class PlayerByUsername
{
    public string Username { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
}

// develop
public class PlayerStatsSnapshot
{
    public Guid PlayerId { get; set; }
    public long TotalPoints { get; set; }
    public long GamesWon { get; set; }
    public long GamesLost { get; set; }
}

public class PlayerInfo
{
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public record UpdatePlayerRequest(string? Username, string? AvatarUrl);

public record PlayerProfileResponse(Guid PlayerId, string Username, string Email, string? AvatarUrl, DateTimeOffset CreatedAt);
