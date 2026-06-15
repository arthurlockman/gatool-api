using System.Text.Json.Serialization;
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

// ---------------------------------------------------------------------------
// Raw FIRST Global API models (deserialized from api.first.global/v1)
// ---------------------------------------------------------------------------

[UsedImplicitly]
public record FgTeam(
    int TeamKey,
    int CardStatus,
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

[UsedImplicitly]
public record FgMatchDetails(
    string? EventKey,
    string? TournamentKey,
    int Id,
    int BarriersInRedMitigator,
    int BarriersInBlueMitigator,
    double BiodiversityUnitsRedSideEcosystem,
    double BiodiversityUnitsCenterEcosystem,
    double BiodiversityUnitsBlueSideEcosystem,
    double BiodiversityDistributionFactor,
    double ApproximateBiodiversityRedSideEcosystem,
    double ApproximateBiodiversityCenterEcosystem,
    double ApproximateBiodiversityBlueSideEcosystem,
    double RedRobotOneParking,
    double RedRobotTwoParking,
    double RedRobotThreeParking,
    double BlueRobotOneParking,
    double BlueRobotTwoParking,
    double BlueRobotThreeParking,
    int Coopertition,
    double BiodiversityDistributed,
    double RedProtectionMultiplier,
    double BlueProtectionMultiplier,
    int AllBarriersCleared);

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

/// <summary>Full score breakdown for a single FIRST Global match.</summary>
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
    List<FgAllianceScore> Alliances);

[UsedImplicitly]
public record FgMatchScoresResponse(
    [property: JsonPropertyName("MatchScores")]
    List<FgMatchScore> MatchScores);
