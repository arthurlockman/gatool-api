using System.Net;
using GAToolAPI.Attributes;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[ApiController]
[Route("v3/{year}/nexus")]
public class NexusController(
    ILogger<NexusController> logger,
    NexusApiService nexusApiClient)
    : ControllerBase
{
    /// <summary>
    ///     Gets the match schedule for an FRC event from the FRC Nexus system, transformed into
    ///     the standard hybrid schedule format. Includes practice, qualification, and playoff matches
    ///     with estimated and actual start times sourced from Nexus.
    /// </summary>
    /// <remarks>
    ///     The Nexus event key is derived from the year and event code (e.g., year=2026, eventCode=mawor1
    ///     becomes event key "2026mawor1"). For practice and qualification matches, teams are listed in
    ///     station order (Red1–Red3, Blue1–Blue3). For playoff matches, teams are listed in alliance order
    ///     (Captain, 1st round pick, 2nd round pick, and optionally 3rd round pick) in stations Red1–Red4
    ///     and Blue1–Blue4.
    /// </remarks>
    /// <param name="year">The competition year/season (e.g., 2026).</param>
    /// <param name="eventCode">The FRC event code (e.g., mawor1). Combined with year to form the Nexus event key.</param>
    /// <returns>Hybrid schedule with practice, qualification, and playoff matches including Nexus timing data.</returns>
    /// <response code="200">Returns the schedule.</response>
    /// <response code="204">No schedule found for the event.</response>
    [HttpGet("schedule/{eventCode}")]
    [RedisCache("nexus:schedule", RedisCacheTime.OneMinute)]
    [OpenApiTag("FRC Nexus")]
    [ProducesResponseType(typeof(HybridScheduleResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetNexusSchedule(string year, string eventCode)
    {
        var eventKey = $"{year}{eventCode.ToLowerInvariant()}";
        var nexusResponse = await nexusApiClient.Get<NexusEventResponse>(eventKey);

        if (nexusResponse == null || nexusResponse.Matches.Count == 0) return NoContent();

        try
        {
            var schedule = BuildSchedule(nexusResponse);
            return Ok(schedule);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building Nexus schedule for event {EventKey}", eventKey);
            return NoContent();
        }
    }

    private static HybridScheduleResponse BuildSchedule(NexusEventResponse nexusResponse)
    {
        var matches = new List<HybridMatch>();

        foreach (var nexusMatch in nexusResponse.Matches)
        {
            var (tournamentLevel, matchNumber) = ParseLabel(nexusMatch.Label);

            var hm = new HybridMatch
            {
                MatchNumber = matchNumber,
                TournamentLevel = tournamentLevel,
                Description = nexusMatch.Label,
                // Prefer the official scheduled start time; fall back to Nexus estimated start time.
                StartTime = MillisToIso(nexusMatch.Times.ScheduledStartTime ?? nexusMatch.Times.EstimatedStartTime),
                // AutoStartTime tracks the Nexus estimated start time (countdown clock source).
                AutoStartTime = MillisToIso(nexusMatch.Times.EstimatedStartTime),
                ActualStartTime = MillisToIso(nexusMatch.Times.ActualOnFieldTime),
                Field = null,
                EventCode = nexusResponse.EventKey,
                Teams = []
            };

            // Red teams: preserve array index for station assignment so that null (unassigned)
            // slots do not shift the station numbers of present teams.
            for (var i = 0; i < nexusMatch.RedTeams.Count; i++)
            {
                var team = nexusMatch.RedTeams[i];
                if (team == null) continue;
                hm.Teams.Add(new HybridTeam
                {
                    TeamNumber = int.TryParse(team, out var tn) ? (object)tn : team,
                    Station = $"Red{i + 1}",
                    Surrogate = false
                });
            }

            // Blue teams: same index-preserving logic.
            for (var i = 0; i < nexusMatch.BlueTeams.Count; i++)
            {
                var team = nexusMatch.BlueTeams[i];
                if (team == null) continue;
                hm.Teams.Add(new HybridTeam
                {
                    TeamNumber = int.TryParse(team, out var tn) ? (object)tn : team,
                    Station = $"Blue{i + 1}",
                    Surrogate = false
                });
            }

            matches.Add(hm);
        }

        return new HybridScheduleResponse
        {
            Schedule = new HybridSchedule { Schedule = matches }
        };
    }

    /// <summary>
    ///     Parses a Nexus match label into a tournament level and match number.
    ///     "Practice N" → ("Practice", N)
    ///     "Qualification N" → ("Qual", N)
    ///     "Playoff N" or "Final N" → ("Playoff", N)
    /// </summary>
    private static (string TournamentLevel, int MatchNumber) ParseLabel(string label)
    {
        if (label.StartsWith("Practice ") && int.TryParse(label["Practice ".Length..], out var pNum))
            return ("Practice", pNum);
        if (label.StartsWith("Qualification ") && int.TryParse(label["Qualification ".Length..], out var qNum))
            return ("Qual", qNum);
        if (label.StartsWith("Playoff ") && int.TryParse(label["Playoff ".Length..], out var plNum))
            return ("Playoff", plNum);
        if (label.StartsWith("Final ") && int.TryParse(label["Final ".Length..], out var fNum))
            return ("Playoff", fNum);
        return ("Unknown", 0);
    }

    private static string? MillisToIso(long? millis) =>
        millis.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(millis.Value).ToString("o") : null;
}
