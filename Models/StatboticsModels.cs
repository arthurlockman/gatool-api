using System.Text.Json.Serialization;

namespace GAToolAPI.Models;

/// <summary>
/// Team statistics and data from Statbotics API
/// </summary>
public class StatboticsTeamData
{
    /// <summary>
    /// Team number
    /// </summary>
    public int Team { get; set; }

    /// <summary>
    /// Competition year
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Team name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Country code
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// State/province code
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// District code (if applicable)
    /// </summary>
    public string? District { get; set; }

    /// <summary>
    /// Team's rookie year
    /// </summary>
    [JsonPropertyName("rookie_year")]
    public int RookieYear { get; set; }

    /// <summary>
    /// Expected Points Added (EPA) statistics
    /// </summary>
    public StatboticsEpa Epa { get; set; } = new();

    /// <summary>
    /// Win-loss record for the season
    /// </summary>
    public StatboticsRecord Record { get; set; } = new();

    /// <summary>
    /// District points earned (if applicable)
    /// </summary>
    [JsonPropertyName("district_points")]
    public int? DistrictPoints { get; set; }

    /// <summary>
    /// District ranking (if applicable)
    /// </summary>
    [JsonPropertyName("district_rank")]
    public int? DistrictRank { get; set; }
}

/// <summary>
/// Expected Points Added (EPA) statistics and breakdowns
/// </summary>
public class StatboticsEpa
{
    /// <summary>
    /// Total points statistics
    /// </summary>
    [JsonPropertyName("total_points")]
    public StatboticsStatistic TotalPoints { get; set; } = new();

    /// <summary>
    /// Unitless EPA rating
    /// </summary>
    public double Unitless { get; set; }

    /// <summary>
    /// Normalized EPA rating
    /// </summary>
    public double Norm { get; set; }

    /// <summary>
    /// Confidence interval [lower, upper]
    /// </summary>
    public double[] Conf { get; set; } = new double[2];

    /// <summary>
    /// Detailed breakdown of EPA by game component
    /// </summary>
    public StatboticsBreakdown Breakdown { get; set; } = new();

    /// <summary>
    /// EPA statistics over time
    /// </summary>
    public StatboticsStats Stats { get; set; } = new();

    /// <summary>
    /// Rankings at different levels (global, country, state, district)
    /// </summary>
    public StatboticsRanks Ranks { get; set; } = new();
}

/// <summary>
/// Statistical value with mean and standard deviation
/// </summary>
public class StatboticsStatistic
{
    /// <summary>
    /// Mean value
    /// </summary>
    public double Mean { get; set; }

    /// <summary>
    /// Standard deviation
    /// </summary>
    public double Sd { get; set; }
}

/// <summary>
/// Detailed breakdown of EPA by game component (2024 Crescendo specific)
/// </summary>
public class StatboticsBreakdown
{
    /// <summary>
    /// Total points contributed
    /// </summary>
    [JsonPropertyName("total_points")]
    public double TotalPoints { get; set; }

    /// <summary>
    /// Auto period points
    /// </summary>
    [JsonPropertyName("auto_points")]
    public double AutoPoints { get; set; }

    /// <summary>
    /// Teleop period points
    /// </summary>
    [JsonPropertyName("teleop_points")]
    public double TeleopPoints { get; set; }

    /// <summary>
    /// Endgame period points
    /// </summary>
    [JsonPropertyName("endgame_points")]
    public double EndgamePoints { get; set; }

    /// <summary>
    /// Melody ranking point contribution
    /// </summary>
    [JsonPropertyName("melody_rp")]
    public double MelodyRp { get; set; }

    /// <summary>
    /// Ensemble ranking point contribution
    /// </summary>
    [JsonPropertyName("ensemble_rp")]
    public double EnsembleRp { get; set; }

    /// <summary>
    /// Tiebreaker points
    /// </summary>
    [JsonPropertyName("tiebreaker_points")]
    public double TiebreakerPoints { get; set; }

    /// <summary>
    /// Auto leave points
    /// </summary>
    [JsonPropertyName("auto_leave_points")]
    public double AutoLeavePoints { get; set; }

    /// <summary>
    /// Auto notes scored
    /// </summary>
    [JsonPropertyName("auto_notes")]
    public double AutoNotes { get; set; }

    /// <summary>
    /// Auto note points
    /// </summary>
    [JsonPropertyName("auto_note_points")]
    public double AutoNotePoints { get; set; }

    /// <summary>
    /// Teleop notes scored
    /// </summary>
    [JsonPropertyName("teleop_notes")]
    public double TeleopNotes { get; set; }

    /// <summary>
    /// Teleop note points
    /// </summary>
    [JsonPropertyName("teleop_note_points")]
    public double TeleopNotePoints { get; set; }

    /// <summary>
    /// Amp notes scored
    /// </summary>
    [JsonPropertyName("amp_notes")]
    public double AmpNotes { get; set; }

    /// <summary>
    /// Amp points
    /// </summary>
    [JsonPropertyName("amp_points")]
    public double AmpPoints { get; set; }

    /// <summary>
    /// Speaker notes scored
    /// </summary>
    [JsonPropertyName("speaker_notes")]
    public double SpeakerNotes { get; set; }

    /// <summary>
    /// Speaker points
    /// </summary>
    [JsonPropertyName("speaker_points")]
    public double SpeakerPoints { get; set; }

    /// <summary>
    /// Amplified notes scored
    /// </summary>
    [JsonPropertyName("amplified_notes")]
    public double AmplifiedNotes { get; set; }

    /// <summary>
    /// Total notes scored
    /// </summary>
    [JsonPropertyName("total_notes")]
    public double TotalNotes { get; set; }

    /// <summary>
    /// Total note points
    /// </summary>
    [JsonPropertyName("total_note_points")]
    public double TotalNotePoints { get; set; }

    /// <summary>
    /// Endgame park points
    /// </summary>
    [JsonPropertyName("endgame_park_points")]
    public double EndgameParkPoints { get; set; }

    /// <summary>
    /// Endgame on stage points
    /// </summary>
    [JsonPropertyName("endgame_on_stage_points")]
    public double EndgameOnStagePoints { get; set; }

    /// <summary>
    /// Endgame harmony points
    /// </summary>
    [JsonPropertyName("endgame_harmony_points")]
    public double EndgameHarmonyPoints { get; set; }

    /// <summary>
    /// Endgame trap points
    /// </summary>
    [JsonPropertyName("endgame_trap_points")]
    public double EndgameTrapPoints { get; set; }

    /// <summary>
    /// Endgame spotlight points
    /// </summary>
    [JsonPropertyName("endgame_spotlight_points")]
    public double EndgameSpotlightPoints { get; set; }

    /// <summary>
    /// First ranking point contribution
    /// </summary>
    [JsonPropertyName("rp_1")]
    public double Rp1 { get; set; }

    /// <summary>
    /// Second ranking point contribution
    /// </summary>
    [JsonPropertyName("rp_2")]
    public double Rp2 { get; set; }
}

/// <summary>
/// EPA statistics over time
/// </summary>
public class StatboticsStats
{
    /// <summary>
    /// EPA at start of season
    /// </summary>
    public double Start { get; set; }

    /// <summary>
    /// EPA before championships
    /// </summary>
    [JsonPropertyName("pre_champs")]
    public double PreChamps { get; set; }

    /// <summary>
    /// Maximum EPA achieved
    /// </summary>
    public double Max { get; set; }
}

/// <summary>
/// Rankings at different competitive levels
/// </summary>
public class StatboticsRanks
{
    /// <summary>
    /// Global ranking
    /// </summary>
    public StatboticsRank Total { get; set; } = new();

    /// <summary>
    /// Country ranking
    /// </summary>
    public StatboticsRank Country { get; set; } = new();

    /// <summary>
    /// State/province ranking
    /// </summary>
    public StatboticsRank State { get; set; } = new();

    /// <summary>
    /// District ranking (if applicable)
    /// </summary>
    public StatboticsRank? District { get; set; }
}

/// <summary>
/// Ranking information at a specific level
/// </summary>
public class StatboticsRank
{
    /// <summary>
    /// Numerical rank
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Percentile ranking (0-1)
    /// </summary>
    public double Percentile { get; set; }

    /// <summary>
    /// Total number of teams in this ranking pool
    /// </summary>
    [JsonPropertyName("team_count")]
    public int TeamCount { get; set; }
}

/// <summary>
/// Win-loss record information
/// </summary>
public class StatboticsRecord
{
    /// <summary>
    /// Number of wins
    /// </summary>
    public int Wins { get; set; }

    /// <summary>
    /// Number of losses
    /// </summary>
    public int Losses { get; set; }

    /// <summary>
    /// Number of ties
    /// </summary>
    public int Ties { get; set; }

    /// <summary>
    /// Total number of matches
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Win rate (0-1)
    /// </summary>
    public double Winrate { get; set; }
}
