using System.Text.Json.Serialization;

namespace GAToolAPI.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoreProgram { FRC, FTC }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoreScope { Global, District, League, Region }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoreType { Overall, PenaltyFree, Offsetting, TBAPenaltyFree, AllianceContribution }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TournamentLevel { Qual, Playoff }

public record HighScore
{
    public string Level { get; init; } = null!;
    public MatchData MatchData { get; init; } = null!;
    public string Type { get; init; } = null!;
    public int Year { get; init; }
    public string YearType { get; init; } = null!;
}

public record MatchData
{
    public EventInfo Event { get; init; } = null!;
    public string HighScoreAlliance { get; init; } = null!;
    public HybridMatch Match { get; init; } = null!;
}

public record EventInfo
{
    public string? DistrictCode { get; init; }
    public string? EventCode { get; init; } = null!;
    public string Type { get; init; } = null!;
}