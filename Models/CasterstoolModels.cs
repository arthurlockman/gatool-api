namespace GAToolAPI.Models;

/// <summary>
///     Team connection data from Casterstool (partnership and opponent history).
/// </summary>
public record TeamConnection
{
    /// <summary>Team A number.</summary>
    public int TeamA { get; init; }

    /// <summary>Team A name.</summary>
    public string TeamAName { get; init; } = string.Empty;

    /// <summary>Team B number.</summary>
    public int TeamB { get; init; }

    /// <summary>Team B name.</summary>
    public string TeamBName { get; init; } = string.Empty;

    /// <summary>Events where these teams were partners.</summary>
    public List<ConnectionEvent> PartneredAt { get; init; } = [];

    /// <summary>Events where these teams were opponents.</summary>
    public List<ConnectionEvent> OpponentsAt { get; init; } = [];
}

/// <summary>
///     Event reference within a team connection.
/// </summary>
public record ConnectionEvent
{
    /// <summary>TBA event key (e.g. 2025rikin).</summary>
    public string EventKey { get; init; } = string.Empty;

    /// <summary>Event display name.</summary>
    public string EventName { get; init; } = string.Empty;

    /// <summary>Competition year.</summary>
    public int Year { get; init; }

    /// <summary>Stage description (e.g. Round 1 (Upper), Finals).</summary>
    public string Stage { get; init; } = string.Empty;

    /// <summary>Match result (for partnered_at; null when not applicable).</summary>
    public object? Result { get; init; }
}
