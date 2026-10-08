using System.Text.Json;
using System.Text.Json.Serialization;
using GAToolAPI.Helpers;
using JetBrains.Annotations;

namespace GAToolAPI.Models;

/// <summary>
///     FRC-compatible team record extended with FIRST Global country code fields.
/// </summary>
[UsedImplicitly]
public record FgFrcTeam(
    int TeamNumber,
    string? NameFull,
    string? NameShort,
    string? City,
    string? StateProv,
    string? Country,
    string? CountryCode,
    int RookieYear,
    string? RobotName,
    string? DistrictCode,
    string? SchoolName,
    string? Website,
    string? HomeCMP);

[UsedImplicitly]
public record FgTeamsResponse(
    int TeamCountTotal,
    int TeamCountPage,
    int PageCurrent,
    int PageTotal,
    List<FgFrcTeam>? Teams);

/// <summary>
///     Optional team filter for the FIRST Global multi-season awards endpoint.
///     Team values are two-character FIRST Global country codes. Most are letters (for example,
///     AL), but FIRST Global also assigns numeric codes to some teams (for example, 10 and 15).
/// </summary>
[UsedImplicitly]
public class FgAwardsQueryRequest
{
    public List<string>? Teams { get; set; }
}

// ---------------------------------------------------------------------------
// Raw FIRST Global API models (deserialized from api.first.global/v1)
// ---------------------------------------------------------------------------

[UsedImplicitly]
public record FgTeam(
    int TeamKey,
    [property: JsonConverter(typeof(FlexibleIntConverter))]
    int CardStatus,
    [property: JsonConverter(typeof(FlexibleIntConverter))]
    int HasCard,
    string Country,
    string CountryCode,
    string Name,
    string ShortName);

[UsedImplicitly]
public record FgParticipant(
    string EventKey,
    string TournamentKey,
    int Id,
    int Station,
    int TeamKey,
    int Disqualified,
    int CardStatus,
    int Surrogate,
    int NoShow);

/// <summary>Complete FIRST Global match details, preserved without season-specific assumptions.</summary>
[UsedImplicitly]
public record FgMatchDetails
{
    [JsonExtensionData] public Dictionary<string, JsonElement> Properties { get; init; } = [];
}

[UsedImplicitly]
public record FgMatch(
    string EventKey,
    string TournamentKey,
    int Id,
    string Name,
    string? ScheduledTime,
    string? StartTime,
    int FieldNumber,
    double CycleTime,
    int RedScore,
    int RedMinPen,
    int RedMajPen,
    int BlueScore,
    int BlueMinPen,
    int BlueMajPen,
    int Result,
    List<FgParticipant>? Participants,
    FgMatchDetails? Details);

[UsedImplicitly]
public record FgRanking(
    string EventKey,
    string TournamentKey,
    int TeamKey,
    int Rank,
    int RankChange,
    int Played,
    int Wins,
    int Losses,
    int Ties,
    double RankingScore,
    int HighestScore,
    double ProtectionPoints);

[UsedImplicitly]
public record FgAllianceMember(
    string? EventKey,
    string? TournamentKey,
    int TeamKey,
    int AllianceRank,
    string? AllianceNameShort,
    string? AllianceNameLong,
    int IsCaptain,
    int PickOrder);

[UsedImplicitly]
public record FgAlliance(
    FgAllianceMember? Captain,
    FgAllianceMember? Pick1,
    FgAllianceMember? Pick2,
    FgAllianceMember? Pick3,
    string? Name,
    double RankingScore,
    int Played,
    string? EventKey,
    int Rank);

/// <summary>
///     A single award recipient. Country-based awards populate <c>Country</c>/<c>CountryCode</c>;
///     individual awards (e.g. mentor recognition) populate <c>RecipientName</c> instead.
///     <c>Class</c> is only present on <c>other</c> entries and denotes which tier (gold/silver/bronze)
///     the entry is tied to, if any.
/// </summary>
[UsedImplicitly]
public record FgAwardRecipient(
    string? Country,
    string? CountryCode,
    string? RecipientName,
    string? Class);

[UsedImplicitly]
public record FgAward(
    string Name,
    string? Description,
    FgAwardRecipient? Gold,
    FgAwardRecipient? Silver,
    FgAwardRecipient? Bronze,
    List<FgAwardRecipient>? Other,
    string? EventKey,
    int SortOrder);

// ---------------------------------------------------------------------------
// Score output models (returned by the /scores endpoint)
// ---------------------------------------------------------------------------

/// <summary>Per-alliance score breakdown for a FIRST Global match.</summary>
[UsedImplicitly]
public record FgAllianceScore(
    string Alliance,
    int TotalPoints,
    int FoulPoints,
    int BarriersInMitigator,
    double RobotOneParking,
    double RobotTwoParking,
    double RobotThreeParking,
    double ProtectionMultiplier,
    double BiodiversityUnits,
    double ApproximateBiodiversity);

/// <summary>Full FIRST Global match score, including unmodeled season-specific detail fields.</summary>
[UsedImplicitly]
public record FgMatchScore(
    string MatchLevel,
    int MatchNumber,
    int WinningAlliance,
    bool CoopertitionAchieved,
    bool AllBarriersCleared,
    double BiodiversityDistributed,
    double BiodiversityDistributionFactor,
    double BiodiversityUnitsCenterEcosystem,
    double ApproximateBiodiversityCenterEcosystem,
    List<FgAllianceScore> Alliances)
{
    public Dictionary<string, JsonElement>? Details { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

/// <summary>FRC-compatible match result extended with FIRST Global field number.</summary>
[UsedImplicitly]
public record FgMatchResult(
    bool IsReplay,
    string? MatchVideoLink,
    string? Description,
    int MatchNumber,
    int? ScoreRedFinal,
    int? ScoreRedFoul,
    int? ScoreRedAuto,
    int? ScoreBlueFinal,
    int? ScoreBlueFoul,
    int? ScoreBlueAuto,
    string? AutoStartTime,
    string? ActualStartTime,
    string? TournamentLevel,
    string? PostResultTime,
    int FieldNumber,
    List<MatchTeam>? Teams);

[UsedImplicitly]
public record FgMatchesResponse(List<FgMatchResult>? Matches);

[UsedImplicitly]
public record FgMatchScoresResponse(
    [property: JsonPropertyName("MatchScores")]
    List<FgMatchScore> MatchScores);