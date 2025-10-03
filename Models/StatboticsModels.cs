using System.Text.Json.Serialization;

namespace GAToolAPI.Models;

/// <summary>
/// Team statistics and data from Statbotics API
/// </summary>
public record StatboticsTeamData
{
    /// <summary>
    /// Team number
    /// </summary>
    public int Team { get; init; }

    /// <summary>
    /// Competition year
    /// </summary>
    public int Year { get; init; }

    /// <summary>
    /// Team name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Country code
    /// </summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>
    /// State/province code
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// District code (if applicable)
    /// </summary>
    public string? District { get; init; }

    /// <summary>
    /// Team's rookie year
    /// </summary>
    [JsonPropertyName("rookie_year")]
    public int RookieYear { get; init; }

    /// <summary>
    /// Expected Points Added (EPA) statistics
    /// </summary>
    public StatboticsEpa Epa { get; init; } = new();

    /// <summary>
    /// Win-loss record for the season
    /// </summary>
    public StatboticsRecord Record { get; init; } = new();

    /// <summary>
    /// District points earned (if applicable)
    /// </summary>
    [JsonPropertyName("district_points")]
    public int? DistrictPoints { get; init; }

    /// <summary>
    /// District ranking (if applicable)
    /// </summary>
    [JsonPropertyName("district_rank")]
    public int? DistrictRank { get; init; }
}

/// <summary>
/// Expected Points Added (EPA) statistics and breakdowns
/// </summary>
public record StatboticsEpa
{
    /// <summary>
    /// Total points statistics
    /// </summary>
    [JsonPropertyName("total_points")]
    public StatboticsStatistic TotalPoints { get; init; } = new();

    /// <summary>
    /// Unitless EPA rating
    /// </summary>
    public double Unitless { get; init; }

    /// <summary>
    /// Normalized EPA rating
    /// </summary>
    public double Norm { get; init; }

    /// <summary>
    /// Confidence interval [lower, upper]
    /// </summary>
    public double[] Conf { get; init; } = new double[2];

    /// <summary>
    /// Detailed breakdown of EPA by game component
    /// </summary>
    public StatboticsBreakdown Breakdown { get; init; } = new();

    /// <summary>
    /// EPA statistics over time
    /// </summary>
    public StatboticsStats Stats { get; init; } = new();

    /// <summary>
    /// Rankings at different levels (global, country, state, district)
    /// </summary>
    public StatboticsRanks Ranks { get; init; } = new();
}

/// <summary>
/// Statistical value with mean and standard deviation
/// </summary>
public record StatboticsStatistic
{
    /// <summary>
    /// Mean value
    /// </summary>
    public double Mean { get; init; }

    /// <summary>
    /// Standard deviation
    /// </summary>
    public double Sd { get; init; }
}

/// <summary>
/// Detailed breakdown of EPA by game component (2024 Crescendo specific)
/// </summary>
public record StatboticsBreakdown
{
    /// <summary>
    /// Total points contributed
    /// </summary>
    [JsonPropertyName("total_points")]
    public double TotalPoints { get; init; }

    /// <summary>
    /// Auto period points
    /// </summary>
    [JsonPropertyName("auto_points")]
    public double AutoPoints { get; init; }

    /// <summary>
    /// Teleop period points
    /// </summary>
    [JsonPropertyName("teleop_points")]
    public double TeleopPoints { get; init; }

    /// <summary>
    /// Endgame period points
    /// </summary>
    [JsonPropertyName("endgame_points")]
    public double EndgamePoints { get; init; }

    /// <summary>
    /// Melody ranking point contribution
    /// </summary>
    [JsonPropertyName("melody_rp")]
    public double MelodyRp { get; init; }

    /// <summary>
    /// Ensemble ranking point contribution
    /// </summary>
    [JsonPropertyName("ensemble_rp")]
    public double EnsembleRp { get; init; }

    /// <summary>
    /// Tiebreaker points
    /// </summary>
    [JsonPropertyName("tiebreaker_points")]
    public double TiebreakerPoints { get; init; }

    /// <summary>
    /// Auto leave points
    /// </summary>
    [JsonPropertyName("auto_leave_points")]
    public double AutoLeavePoints { get; init; }

    /// <summary>
    /// Auto notes scored
    /// </summary>
    [JsonPropertyName("auto_notes")]
    public double AutoNotes { get; init; }

    /// <summary>
    /// Auto note points
    /// </summary>
    [JsonPropertyName("auto_note_points")]
    public double AutoNotePoints { get; init; }

    /// <summary>
    /// Teleop notes scored
    /// </summary>
    [JsonPropertyName("teleop_notes")]
    public double TeleopNotes { get; init; }

    /// <summary>
    /// Teleop note points
    /// </summary>
    [JsonPropertyName("teleop_note_points")]
    public double TeleopNotePoints { get; init; }

    /// <summary>
    /// Amp notes scored
    /// </summary>
    [JsonPropertyName("amp_notes")]
    public double AmpNotes { get; init; }

    /// <summary>
    /// Amp points
    /// </summary>
    [JsonPropertyName("amp_points")]
    public double AmpPoints { get; init; }

    /// <summary>
    /// Speaker notes scored
    /// </summary>
    [JsonPropertyName("speaker_notes")]
    public double SpeakerNotes { get; init; }

    /// <summary>
    /// Speaker points
    /// </summary>
    [JsonPropertyName("speaker_points")]
    public double SpeakerPoints { get; init; }

    /// <summary>
    /// Amplified notes scored
    /// </summary>
    [JsonPropertyName("amplified_notes")]
    public double AmplifiedNotes { get; init; }

    /// <summary>
    /// Total notes scored
    /// </summary>
    [JsonPropertyName("total_notes")]
    public double TotalNotes { get; init; }

    /// <summary>
    /// Total note points
    /// </summary>
    [JsonPropertyName("total_note_points")]
    public double TotalNotePoints { get; init; }

    /// <summary>
    /// Endgame park points
    /// </summary>
    [JsonPropertyName("endgame_park_points")]
    public double EndgameParkPoints { get; init; }

    /// <summary>
    /// Endgame on stage points
    /// </summary>
    [JsonPropertyName("endgame_on_stage_points")]
    public double EndgameOnStagePoints { get; init; }

    /// <summary>
    /// Endgame harmony points
    /// </summary>
    [JsonPropertyName("endgame_harmony_points")]
    public double EndgameHarmonyPoints { get; init; }

    /// <summary>
    /// Endgame trap points
    /// </summary>
    [JsonPropertyName("endgame_trap_points")]
    public double EndgameTrapPoints { get; init; }

    /// <summary>
    /// Endgame spotlight points
    /// </summary>
    [JsonPropertyName("endgame_spotlight_points")]
    public double EndgameSpotlightPoints { get; init; }

    /// <summary>
    /// First ranking point contribution
    /// </summary>
    [JsonPropertyName("rp_1")]
    public double Rp1 { get; init; }

    /// <summary>
    /// Second ranking point contribution
    /// </summary>
    [JsonPropertyName("rp_2")]
    public double Rp2 { get; init; }
}

/// <summary>
/// EPA statistics over time
/// </summary>
public record StatboticsStats
{
    /// <summary>
    /// EPA at start of season
    /// </summary>
    public double Start { get; init; }

    /// <summary>
    /// EPA before championships
    /// </summary>
    [JsonPropertyName("pre_champs")]
    public double PreChamps { get; init; }

    /// <summary>
    /// Maximum EPA achieved
    /// </summary>
    public double Max { get; init; }
}

/// <summary>
/// Rankings at different competitive levels
/// </summary>
public record StatboticsRanks
{
    /// <summary>
    /// Global ranking
    /// </summary>
    public StatboticsRank Total { get; init; } = new();

    /// <summary>
    /// Country ranking
    /// </summary>
    public StatboticsRank Country { get; init; } = new();

    /// <summary>
    /// State/province ranking
    /// </summary>
    public StatboticsRank State { get; init; } = new();

    /// <summary>
    /// District ranking (if applicable)
    /// </summary>
    public StatboticsRank? District { get; init; }
}

/// <summary>
/// Ranking information at a specific level
/// </summary>
public record StatboticsRank
{
    /// <summary>
    /// Numerical rank
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Percentile ranking (0-1)
    /// </summary>
    public double Percentile { get; init; }

    /// <summary>
    /// Total number of teams in this ranking pool
    /// </summary>
    [JsonPropertyName("team_count")]
    public int TeamCount { get; init; }
}

/// <summary>
/// Win-loss record information
/// </summary>
public record StatboticsRecord
{
    /// <summary>
    /// Number of wins
    /// </summary>
    public int Wins { get; init; }

    /// <summary>
    /// Number of losses
    /// </summary>
    public int Losses { get; init; }

    /// <summary>
    /// Number of ties
    /// </summary>
    public int Ties { get; init; }

    /// <summary>
    /// Total number of matches
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Win rate (0-1)
    /// </summary>
    public double Winrate { get; init; }
}
