using System.Text.Json.Serialization;

namespace GAToolAPI.Models;

/// <summary>
/// FTC Scout team event data
/// </summary>
public class FtcScoutEventData
{
    /// <summary>
    /// The competition season/year
    /// </summary>
    public int Season { get; set; }

    /// <summary>
    /// The event code
    /// </summary>
    public string EventCode { get; set; } = string.Empty;

    /// <summary>
    /// The team number
    /// </summary>
    public int TeamNumber { get; set; }

    /// <summary>
    /// Whether this is a remote event
    /// </summary>
    public bool IsRemote { get; set; }

    /// <summary>
    /// Team statistics for this event (null if no data available)
    /// </summary>
    public FtcScoutEventStats? Stats { get; set; }

    /// <summary>
    /// When this record was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Detailed statistics for a team at an event
/// </summary>
public class FtcScoutEventStats
{
    /// <summary>
    /// Team's rank at the event
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Ranking points
    /// </summary>
    public double Rp { get; set; }

    /// <summary>
    /// First tiebreaker
    /// </summary>
    public double Tb1 { get; set; }

    /// <summary>
    /// Second tiebreaker
    /// </summary>
    public double Tb2 { get; set; }

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
    /// Number of disqualifications
    /// </summary>
    public int Dqs { get; set; }

    /// <summary>
    /// Number of qualification matches played
    /// </summary>
    public int QualMatchesPlayed { get; set; }

    /// <summary>
    /// Total statistics across all matches
    /// </summary>
    public FtcScoutStatistics Tot { get; set; } = new();

    /// <summary>
    /// Average statistics per match
    /// </summary>
    public FtcScoutStatistics Avg { get; set; } = new();

    /// <summary>
    /// OPR (Offensive Power Rating) statistics
    /// </summary>
    public FtcScoutStatistics Opr { get; set; } = new();

    /// <summary>
    /// Minimum values achieved
    /// </summary>
    public FtcScoutStatistics Min { get; set; } = new();

    /// <summary>
    /// Maximum values achieved
    /// </summary>
    public FtcScoutStatistics Max { get; set; } = new();

    /// <summary>
    /// Standard deviation of statistics
    /// </summary>
    public FtcScoutStatistics Dev { get; set; } = new();
}

/// <summary>
/// Detailed FTC scoring statistics
/// </summary>
public class FtcScoutStatistics
{
    // Auto Period - Parking
    /// <summary>
    /// Auto parking points
    /// </summary>
    public double AutoParkPoints { get; set; }

    /// <summary>
    /// Auto parking points (individual)
    /// </summary>
    public double AutoParkPointsIndividual { get; set; }

    // Auto Period - Samples
    /// <summary>
    /// Auto sample points
    /// </summary>
    public double AutoSamplePoints { get; set; }

    /// <summary>
    /// Auto sample net points
    /// </summary>
    public double AutoSampleNetPoints { get; set; }

    /// <summary>
    /// Auto sample low basket points
    /// </summary>
    public double AutoSampleLowPoints { get; set; }

    /// <summary>
    /// Auto sample high basket points
    /// </summary>
    public double AutoSampleHighPoints { get; set; }

    // Auto Period - Specimens
    /// <summary>
    /// Auto specimen points
    /// </summary>
    public double AutoSpecimenPoints { get; set; }

    /// <summary>
    /// Auto specimen low chamber points
    /// </summary>
    public double AutoSpecimenLowPoints { get; set; }

    /// <summary>
    /// Auto specimen high chamber points
    /// </summary>
    public double AutoSpecimenHighPoints { get; set; }

    // Driver Controlled Period - Parking
    /// <summary>
    /// Driver controlled parking points
    /// </summary>
    public double DcParkPoints { get; set; }

    /// <summary>
    /// Driver controlled parking points (individual)
    /// </summary>
    public double DcParkPointsIndividual { get; set; }

    // Driver Controlled Period - Samples
    /// <summary>
    /// Driver controlled sample points
    /// </summary>
    public double DcSamplePoints { get; set; }

    /// <summary>
    /// Driver controlled sample net points
    /// </summary>
    public double DcSampleNetPoints { get; set; }

    /// <summary>
    /// Driver controlled sample low basket points
    /// </summary>
    public double DcSampleLowPoints { get; set; }

    /// <summary>
    /// Driver controlled sample high basket points
    /// </summary>
    public double DcSampleHighPoints { get; set; }

    // Driver Controlled Period - Specimens
    /// <summary>
    /// Driver controlled specimen points
    /// </summary>
    public double DcSpecimenPoints { get; set; }

    /// <summary>
    /// Driver controlled specimen low chamber points
    /// </summary>
    public double DcSpecimenLowPoints { get; set; }

    /// <summary>
    /// Driver controlled specimen high chamber points
    /// </summary>
    public double DcSpecimenHighPoints { get; set; }

    // Period Totals
    /// <summary>
    /// Total auto period points
    /// </summary>
    public double AutoPoints { get; set; }

    /// <summary>
    /// Total driver controlled period points
    /// </summary>
    public double DcPoints { get; set; }

    // Penalties
    /// <summary>
    /// Major penalty points committed by team
    /// </summary>
    public double MajorsCommittedPoints { get; set; }

    /// <summary>
    /// Minor penalty points committed by team
    /// </summary>
    public double MinorsCommittedPoints { get; set; }

    /// <summary>
    /// Total penalty points committed by team
    /// </summary>
    public double PenaltyPointsCommitted { get; set; }

    /// <summary>
    /// Major penalty points committed by opponents
    /// </summary>
    public double MajorsByOppPoints { get; set; }

    /// <summary>
    /// Minor penalty points committed by opponents
    /// </summary>
    public double MinorsByOppPoints { get; set; }

    /// <summary>
    /// Total penalty points committed by opponents
    /// </summary>
    public double PenaltyPointsByOpp { get; set; }

    // Match Totals
    /// <summary>
    /// Total points excluding penalties
    /// </summary>
    public double TotalPointsNp { get; set; }

    /// <summary>
    /// Total points including penalties
    /// </summary>
    public double TotalPoints { get; set; }
}

/// <summary>
/// Quick statistics summary for a team from FTC Scout
/// </summary>
public class FtcScoutQuickStats
{
    /// <summary>
    /// The competition season/year
    /// </summary>
    public int Season { get; set; }

    /// <summary>
    /// Team number
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Total OPR statistics (overall performance rating)
    /// </summary>
    public FtcScoutStatRank Tot { get; set; } = new();

    /// <summary>
    /// Auto period OPR statistics
    /// </summary>
    public FtcScoutStatRank Auto { get; set; } = new();

    /// <summary>
    /// Driver controlled period OPR statistics
    /// </summary>
    public FtcScoutStatRank Dc { get; set; } = new();

    /// <summary>
    /// End game OPR statistics
    /// </summary>
    public FtcScoutStatRank Eg { get; set; } = new();

    /// <summary>
    /// Total number of data points used in calculation
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Statistical value with ranking information
/// </summary>
public class FtcScoutStatRank
{
    /// <summary>
    /// The statistical value (OPR rating)
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// The team's rank for this statistic
    /// </summary>
    public int Rank { get; set; }
}
