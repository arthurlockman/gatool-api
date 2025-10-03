namespace GAToolAPI.Models;

/// <summary>
/// FTC Scout team event data
/// </summary>
public record FtcScoutEventData
{
    /// <summary>
    /// The competition season/year
    /// </summary>
    public int? Season { get; init; }

    /// <summary>
    /// The event code
    /// </summary>
    public string? EventCode { get; init; } = string.Empty;

    /// <summary>
    /// The team number
    /// </summary>
    public int? TeamNumber { get; init; }

    /// <summary>
    /// Whether this is a remote event
    /// </summary>
    public bool? IsRemote { get; init; }

    /// <summary>
    /// Team statistics for this event (null if no data available)
    /// </summary>
    public FtcScoutEventStats? Stats { get; init; }

    /// <summary>
    /// When this record was created
    /// </summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// When this record was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Detailed statistics for a team at an event
/// </summary>
public record FtcScoutEventStats
{
    /// <summary>
    /// Team's rank at the event
    /// </summary>
    public int? Rank { get; init; }

    /// <summary>
    /// Ranking points
    /// </summary>
    public double? Rp { get; init; }

    /// <summary>
    /// First tiebreaker
    /// </summary>
    public double? Tb1 { get; init; }

    /// <summary>
    /// Second tiebreaker
    /// </summary>
    public double? Tb2 { get; init; }

    /// <summary>
    /// Number of wins
    /// </summary>
    public int? Wins { get; init; }

    /// <summary>
    /// Number of losses
    /// </summary>
    public int? Losses { get; init; }

    /// <summary>
    /// Number of ties
    /// </summary>
    public int? Ties { get; init; }

    /// <summary>
    /// Number of disqualifications
    /// </summary>
    public int? Dqs { get; init; }

    /// <summary>
    /// Number of qualification matches played
    /// </summary>
    public int? QualMatchesPlayed { get; init; }

    /// <summary>
    /// Total statistics across all matches
    /// </summary>
    public FtcScoutStatistics? Tot { get; init; } = new();

    /// <summary>
    /// Average statistics per match
    /// </summary>
    public FtcScoutStatistics? Avg { get; init; } = new();

    /// <summary>
    /// OPR (Offensive Power Rating) statistics
    /// </summary>
    public FtcScoutStatistics? Opr { get; init; } = new();

    /// <summary>
    /// Minimum values achieved
    /// </summary>
    public FtcScoutStatistics? Min { get; init; } = new();

    /// <summary>
    /// Maximum values achieved
    /// </summary>
    public FtcScoutStatistics? Max { get; init; } = new();

    /// <summary>
    /// Standard deviation of statistics
    /// </summary>
    public FtcScoutStatistics? Dev { get; init; } = new();
}

/// <summary>
/// Detailed FTC scoring statistics
/// </summary>
public record FtcScoutStatistics
{
    // Auto Period - Parking
    /// <summary>
    /// Auto parking points
    /// </summary>
    public double? AutoParkPoints { get; init; }

    /// <summary>
    /// Auto parking points (individual)
    /// </summary>
    public double? AutoParkPointsIndividual { get; init; }

    // Auto Period - Samples
    /// <summary>
    /// Auto sample points
    /// </summary>
    public double? AutoSamplePoints { get; init; }

    /// <summary>
    /// Auto sample net points
    /// </summary>
    public double? AutoSampleNetPoints { get; init; }

    /// <summary>
    /// Auto sample low basket points
    /// </summary>
    public double? AutoSampleLowPoints { get; init; }

    /// <summary>
    /// Auto sample high basket points
    /// </summary>
    public double? AutoSampleHighPoints { get; init; }

    // Auto Period - Specimens
    /// <summary>
    /// Auto specimen points
    /// </summary>
    public double? AutoSpecimenPoints { get; init; }

    /// <summary>
    /// Auto specimen low chamber points
    /// </summary>
    public double? AutoSpecimenLowPoints { get; init; }

    /// <summary>
    /// Auto specimen high chamber points
    /// </summary>
    public double? AutoSpecimenHighPoints { get; init; }

    // Driver Controlled Period - Parking
    /// <summary>
    /// Driver controlled parking points
    /// </summary>
    public double? DcParkPoints { get; init; }

    /// <summary>
    /// Driver controlled parking points (individual)
    /// </summary>
    public double? DcParkPointsIndividual { get; init; }

    // Driver Controlled Period - Samples
    /// <summary>
    /// Driver controlled sample points
    /// </summary>
    public double? DcSamplePoints { get; init; }

    /// <summary>
    /// Driver controlled sample net points
    /// </summary>
    public double? DcSampleNetPoints { get; init; }

    /// <summary>
    /// Driver controlled sample low basket points
    /// </summary>
    public double? DcSampleLowPoints { get; init; }

    /// <summary>
    /// Driver controlled sample high basket points
    /// </summary>
    public double? DcSampleHighPoints { get; init; }

    // Driver Controlled Period - Specimens
    /// <summary>
    /// Driver controlled specimen points
    /// </summary>
    public double? DcSpecimenPoints { get; init; }

    /// <summary>
    /// Driver controlled specimen low chamber points
    /// </summary>
    public double? DcSpecimenLowPoints { get; init; }

    /// <summary>
    /// Driver controlled specimen high chamber points
    /// </summary>
    public double? DcSpecimenHighPoints { get; init; }

    // Period Totals
    /// <summary>
    /// Total auto period points
    /// </summary>
    public double? AutoPoints { get; init; }

    /// <summary>
    /// Total driver controlled period points
    /// </summary>
    public double? DcPoints { get; init; }

    // Penalties
    /// <summary>
    /// Major penalty points committed by team
    /// </summary>
    public double? MajorsCommittedPoints { get; init; }

    /// <summary>
    /// Minor penalty points committed by team
    /// </summary>
    public double? MinorsCommittedPoints { get; init; }

    /// <summary>
    /// Total penalty points committed by team
    /// </summary>
    public double? PenaltyPointsCommitted { get; init; }

    /// <summary>
    /// Major penalty points committed by opponents
    /// </summary>
    public double? MajorsByOppPoints { get; init; }

    /// <summary>
    /// Minor penalty points committed by opponents
    /// </summary>
    public double? MinorsByOppPoints { get; init; }

    /// <summary>
    /// Total penalty points committed by opponents
    /// </summary>
    public double? PenaltyPointsByOpp { get; init; }

    // Match Totals
    /// <summary>
    /// Total points excluding penalties
    /// </summary>
    public double? TotalPointsNp { get; init; }

    /// <summary>
    /// Total points including penalties
    /// </summary>
    public double? TotalPoints { get; init; }
}

/// <summary>
/// Quick statistics summary for a team from FTC Scout
/// </summary>
public record FtcScoutQuickStats
{
    /// <summary>
    /// The competition season/year
    /// </summary>
    public int? Season { get; init; }

    /// <summary>
    /// Team number
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// Total OPR statistics (overall performance rating)
    /// </summary>
    public FtcScoutStatRank? Tot { get; init; } = new();

    /// <summary>
    /// Auto period OPR statistics
    /// </summary>
    public FtcScoutStatRank? Auto { get; init; } = new();

    /// <summary>
    /// Driver controlled period OPR statistics
    /// </summary>
    public FtcScoutStatRank? Dc { get; init; } = new();

    /// <summary>
    /// End game OPR statistics
    /// </summary>
    public FtcScoutStatRank? Eg { get; init; } = new();

    /// <summary>
    /// Total number of data points used in calculation
    /// </summary>
    public int? Count { get; init; }
}

/// <summary>
/// Statistical value with ranking information
/// </summary>
public record FtcScoutStatRank
{
    /// <summary>
    /// The statistical value (OPR rating)
    /// </summary>
    public double? Value { get; init; }

    /// <summary>
    /// The team's rank for this statistic
    /// </summary>
    public int? Rank { get; init; }
}
