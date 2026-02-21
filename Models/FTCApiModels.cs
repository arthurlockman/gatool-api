using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace GAToolAPI.Models;

[UsedImplicitly]
public record FTCEvent(
    string Code,
    string Name,
    string? Type,
    string? RegionCode,
    string? LeagueCode,
    string? Venue,
    string? Address,
    string? City,
    string? Stateprov,
    string? Country,
    string? Website,
    string? Timezone,
    string DateStart,
    string DateEnd);

[UsedImplicitly]
public record FTCEventListResponse(
    [property: JsonPropertyName("events")] List<FTCEvent>? Events,
    [property: JsonPropertyName("eventCount")]
    int EventCount);

[UsedImplicitly]
public record FTCLeague(
    string? Region,
    string? Code,
    string? Name,
    string? ParentLeagueCode,
    bool? Remote);

[UsedImplicitly]
public record FTCLeagueListResponse(
    [property: JsonPropertyName("leagues")] List<FTCLeague>? Leagues,
    [property: JsonPropertyName("leagueCount")]
    int LeagueCount);

[UsedImplicitly]
public record FTCScheduleTeam(
    [property: JsonPropertyName("teamNumber")]
    int? TeamNumber,
    [property: JsonPropertyName("station")]
    string? Station,
    [property: JsonPropertyName("surrogate")]
    bool Surrogate,
    [property: JsonPropertyName("dq")] object? Dq);

[UsedImplicitly]
public record FTCHybridMatch(
    [property: JsonPropertyName("actualStartTime")]
    string? ActualStartTime,
    [property: JsonPropertyName("description")]
    string? Description,
    [property: JsonPropertyName("tournamentLevel")]
    string? TournamentLevel,
    [property: JsonPropertyName("matchNumber")]
    int MatchNumber,
    [property: JsonPropertyName("startTime")]
    string? StartTime,
    [property: JsonPropertyName("postResultTime")]
    string? PostResultTime,
    [property: JsonPropertyName("scoreRedFinal")]
    int? ScoreRedFinal,
    [property: JsonPropertyName("scoreRedFoul")]
    int? ScoreRedFoul,
    [property: JsonPropertyName("scoreRedAuto")]
    int? ScoreRedAuto,
    [property: JsonPropertyName("scoreBlueFinal")]
    int? ScoreBlueFinal,
    [property: JsonPropertyName("scoreBlueFoul")]
    int? ScoreBlueFoul,
    [property: JsonPropertyName("scoreBlueAuto")]
    int? ScoreBlueAuto,
    [property: JsonPropertyName("teams")]
    List<FTCScheduleTeam>? Teams,
    [property: JsonPropertyName("field")]
    string? Field,
    [property: JsonPropertyName("isReplay")]
    bool? IsReplay,
    [property: JsonPropertyName("matchVideoLink")]
    string? MatchVideoLink);

[UsedImplicitly]
public record FTCHybridScheduleResponse(
    [property: JsonPropertyName("schedule")]
    List<FTCHybridMatch>? Schedule);
