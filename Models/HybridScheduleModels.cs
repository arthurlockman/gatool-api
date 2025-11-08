using System.Text.Json.Serialization;

namespace GAToolAPI.Models;

public class HybridTeam
{
    public object TeamNumber { get; set; } = 0;
    public string Station { get; set; } = null!;
    public bool Surrogate { get; set; }
    public bool Dq { get; set; }
}

public class HybridMatch
{
    public string? Field { get; set; }
    public string? StartTime { get; set; }
    public string? AutoStartTime { get; set; }
    public string? MatchVideoLink { get; set; }
    public int MatchNumber { get; set; }
    public bool? IsReplay { get; set; }
    public string? ActualStartTime { get; set; }
    public string? TournamentLevel { get; set; }
    public string? PostResultTime { get; set; }
    public string? Description { get; set; }
    public int? ScoreRedFinal { get; set; }
    public int? ScoreRedFoul { get; set; }
    public int? ScoreRedAuto { get; set; }
    public int? ScoreBlueFinal { get; set; }
    public int? ScoreBlueFoul { get; set; }
    public int? ScoreBlueAuto { get; set; }
    public List<HybridTeam> Teams { get; set; } = new();
    public string? EventCode { get; set; }
    public string? DistrictCode { get; set; }
    // Transformed score breakdown in FIRST API format
    public MatchScore? MatchScores { get; set; }
}

public class HybridSchedule
{
    [JsonPropertyName("schedule")] public List<HybridMatch> Schedule { get; set; } = [];
}

public class HybridScheduleResponse
{
    [JsonPropertyName("Schedule")] public HybridSchedule Schedule { get; set; } = null!;
}