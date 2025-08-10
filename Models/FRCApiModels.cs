using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace GAToolAPI.Models;

[UsedImplicitly]
public record ScheduleTeam(int? TeamNumber, string? Station, bool Surrogate);

[UsedImplicitly]
public record ScheduleMatch(
    string? Field,
    string? TournamentLevel,
    string? Description,
    string? StartTime,
    int MatchNumber,
    List<ScheduleTeam>? Teams);

[UsedImplicitly]
public record ScheduleResponse(List<ScheduleMatch>? Schedule);

[UsedImplicitly]
public record Team(int TeamNumber, string Station, bool Dq);

[UsedImplicitly]
public record Match(
    string? ActualStartTime,
    string? AutoStartTime,
    string? TournamentLevel,
    string? PostResultTime,
    string? Description,
    bool? IsReplay,
    string? MatchVideoLink,
    int MatchNumber,
    int? ScoreRedFinal,
    int? ScoreRedFoul,
    int? ScoreRedAuto,
    int? ScoreBlueFinal,
    int? ScoreBlueFoul,
    int? ScoreBlueAuto,
    List<Team>? Teams);

[UsedImplicitly]
public record MatchResponse(List<Match>? Matches);

[UsedImplicitly]
public record Event(
    string? AllianceCount,
    int? WeekNumber,
    List<string>? Announcements,
    string Address,
    string? Website,
    List<string>? Webcasts,
    string Timezone,
    string Code,
    string? DivisionCode,
    string Name,
    string Type,
    string? DistrictCode,
    string Venue,
    string City,
    string Stateprov,
    string Country,
    string DateStart, // ISO 8601 date format
    string DateEnd // ISO 8601 date format
);

[UsedImplicitly]
public record EventListResponse(List<Event>? Events, int EventCount);

[UsedImplicitly]
public record DistrictListResponse(List<District>? Districts);

[UsedImplicitly]
public record District(string? Code, string? Name);

[UsedImplicitly]
public record DistrictsResponse(List<District>? Districts, int DistrictCount);

[UsedImplicitly]
public record MatchTeam(int TeamNumber, string? Station, bool Dq);

[UsedImplicitly]
public record MatchResult(
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
    List<MatchTeam>? Teams);

[UsedImplicitly]
public record MatchesResponse(List<MatchResult>? Matches);

[UsedImplicitly]
public record TeamsResponse(
    int TeamCountTotal,
    int TeamCountPage,
    int PageCurrent,
    int PageTotal,
    List<FrcTeam>? Teams
);

[UsedImplicitly]
public record FrcTeam(
    int TeamNumber,
    string? NameFull,
    string? NameShort,
    string? City,
    string? StateProv,
    string? Country,
    int RookieYear,
    string? RobotName,
    string? DistrictCode,
    string? SchoolName,
    string? Website,
    string? HomeCMP);

[UsedImplicitly]
public record Award(
    int? AwardId,
    int? TeamId,
    int? EventId,
    int? EventDivisionId,
    string? EventCode,
    string? Name,
    int? Series,
    int? TeamNumber,
    string? SchoolName,
    string? FullTeamName,
    string? Person,
    bool? CmpQualifying,
    string? CmpQualifyingReason);

[UsedImplicitly]
public record EventAwardsResponse([property: JsonPropertyName("Awards")] List<Award>? Awards);

[UsedImplicitly]
public record ReefRow(
    bool NodeA,
    bool NodeB,
    bool NodeC,
    bool NodeD,
    bool NodeE,
    bool NodeF,
    bool NodeG,
    bool NodeH,
    bool NodeI,
    bool NodeJ,
    bool NodeK,
    bool NodeL);

[UsedImplicitly]
public record Reef(ReefRow? TopRow, ReefRow? MidRow, ReefRow? BotRow, int Trough);

[UsedImplicitly]
public record AllianceScore(
    string? Alliance,
    string? AutoLineRobot1,
    string? EndGameRobot1,
    string? AutoLineRobot2,
    string? EndGameRobot2,
    string? AutoLineRobot3,
    string? EndGameRobot3,
    Reef? AutoReef,
    int AutoCoralCount,
    int AutoMobilityPoints,
    int AutoPoints,
    int AutoCoralPoints,
    Reef? TeleopReef,
    int TeleopCoralCount,
    int TeleopPoints,
    int TeleopCoralPoints,
    int AlgaePoints,
    int NetAlgaeCount,
    int WallAlgaeCount,
    int EndGameBargePoints,
    bool AutoBonusAchieved,
    bool CoralBonusAchieved,
    bool BargeBonusAchieved,
    bool CoopertitionCriteriaMet,
    int FoulCount,
    int TechFoulCount,
    bool G206Penalty,
    bool G410Penalty,
    bool G418Penalty,
    bool G428Penalty,
    int AdjustPoints,
    int FoulPoints,
    int Rp,
    int TotalPoints);

[UsedImplicitly]
public record Tiebreaker(int Item1, string Item2);

[UsedImplicitly]
public record MatchScore(
    string MatchLevel,
    int? MatchNumber,
    int? WinningAlliance,
    Tiebreaker? Tiebreaker,
    bool CoopertitionBonusAchieved,
    int? CoralBonusLevelsThresholdCoop,
    int? CoralBonusLevelsThresholdNonCoop,
    int? CoralBonusLevelsThreshold,
    int? BargeBonusThreshold,
    int? AutoBonusCoralThreshold,
    List<AllianceScore>? Alliances);

[UsedImplicitly]
public record MatchScoresResponse(
    [property: JsonPropertyName("MatchScores")]
    List<MatchScore>? MatchScores);

[UsedImplicitly]
public record TeamAwardsResponse(List<Award>? Awards);

[UsedImplicitly]
public record MediaDetails(
    [property: JsonPropertyName("base64Image")]
    string? Base64Image);

[UsedImplicitly]
public record TeamMedia(
    MediaDetails? Details,
    [property: JsonPropertyName("direct_url")]
    string? DirectUrl,
    [property: JsonPropertyName("foreign_key")]
    string? ForeignKey,
    bool Preferred,
    [property: JsonPropertyName("team_keys")]
    List<string>? TeamKeys,
    string? Type,
    [property: JsonPropertyName("view_url")]
    string? ViewUrl);

[UsedImplicitly]
public record TeamRanking(
    int Rank,
    int TeamNumber,
    double SortOrder1,
    double SortOrder2,
    double SortOrder3,
    double SortOrder4,
    double SortOrder5,
    double SortOrder6,
    int Wins,
    int Losses,
    int Ties,
    double QualAverage,
    int Dq,
    int MatchesPlayed);

[UsedImplicitly]
public record RankingsData(List<TeamRanking>? Rankings);

[UsedImplicitly]
public record RankingsResponse(RankingsData Rankings, object? Headers);

[UsedImplicitly]
public record Alliance(
    int Number,
    int Captain,
    int Round1,
    int Round2,
    int? Round3,
    int? Backup,
    int? BackupReplaced,
    string? Name);

[UsedImplicitly]
public record AlliancesResponse(
    List<Alliance>? Alliances,
    int Count);

[UsedImplicitly]
public record OffseasonTeam(
    int TeamNumber,
    string? NameFull,
    string? NameShort,
    string? SchoolName,
    string? City,
    string? StateProv,
    string? Country,
    string? Website,
    int RookieYear,
    string? RobotName,
    string? DistrictCode,
    string? HomeCMP);

[UsedImplicitly]
public record OffseasonTeamsResponse(
    List<OffseasonTeam>? Teams,
    int TeamCountTotal,
    int TeamCountPage,
    int PageCurrent,
    int PageTotal);

[UsedImplicitly]
public record DistrictRank(
    string DistrictCode,
    int TeamNumber,
    int Rank,
    int TotalPoints,
    string? Event1Code,
    double? Event1Points,
    string? Event2Code,
    double? Event2Points,
    string? DistrictCmpCode,
    double? DistrictCmpPoints,
    int TeamAgePoints,
    int AdjustmentPoints,
    bool QualifiedDistrictCmp,
    bool QualifiedFirstCmp);

[UsedImplicitly]
public record DistrictRankingsResponse(
    List<DistrictRank>? DistrictRanks,
    int RankingCountTotal,
    int RankingCountPage,
    int PageCurrent,
    int PageTotal);

[UsedImplicitly]
public record OffseasonEvent(
    string Code,
    string DivisionCode,
    string Name,
    string Type,
    string? DistrictCode,
    string Venue,
    string Address,
    string City,
    string Stateprov,
    string Country,
    string? Website,
    string Timezone,
    string DateStart,
    string DateEnd);

[UsedImplicitly]
public record OffseasonEventsResponse(List<OffseasonEvent>? Events, int EventCount);