using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace GAToolAPI.Models;

[UsedImplicitly]
public record NexusTimes(
    [property: JsonPropertyName("scheduledStartTime")] long? ScheduledStartTime,
    [property: JsonPropertyName("estimatedQueueTime")] long? EstimatedQueueTime,
    [property: JsonPropertyName("estimatedOnDeckTime")] long? EstimatedOnDeckTime,
    [property: JsonPropertyName("estimatedOnFieldTime")] long? EstimatedOnFieldTime,
    [property: JsonPropertyName("estimatedStartTime")] long? EstimatedStartTime,
    [property: JsonPropertyName("actualQueueTime")] long? ActualQueueTime,
    [property: JsonPropertyName("actualOnDeckTime")] long? ActualOnDeckTime,
    [property: JsonPropertyName("actualOnFieldTime")] long? ActualOnFieldTime
);

[UsedImplicitly]
public record NexusMatch(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("redTeams")] List<string?> RedTeams,
    [property: JsonPropertyName("blueTeams")] List<string?> BlueTeams,
    [property: JsonPropertyName("times")] NexusTimes Times,
    [property: JsonPropertyName("breakAfter")] string? BreakAfter
);

[UsedImplicitly]
public record NexusEventResponse(
    [property: JsonPropertyName("eventKey")] string EventKey,
    [property: JsonPropertyName("dataAsOfTime")] long DataAsOfTime,
    [property: JsonPropertyName("matches")] List<NexusMatch> Matches
);
