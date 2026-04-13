using System.Net;
using System.Text.Json.Nodes;
using GAToolAPI.Attributes;
using GAToolAPI.Extensions;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[Route("v3/{year:int}/highscores")]
[OpenApiTag("High Scores")]
public class HighScoresController(FRCApiService frcApi, ScheduleService schedule, HighScoreRepository highScoreRepository)
    : ControllerBase
{
    /// <summary>
    ///     Gets all FRC high scores for the season (overall, penalty-free, etc.) across all events.
    /// </summary>
    /// <param name="year">The competition year/season.</param>
    /// <returns>List of high score entries (excludes FTC high scores).</returns>
    /// <response code="200">Returns the high scores list.</response>
    [HttpGet]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScores(int year)
    {
        var frcScores = await highScoreRepository.GetHighScores(year, ScoreProgram.FRC, ScoreScope.Global);
        return Ok(frcScores);
    }

    /// <summary>
    ///     Gets calculated high scores (overall, penalty-free, etc.) for a specific FRC event.
    /// </summary>
    /// <param name="year">The competition year/season.</param>
    /// <param name="eventCode">The event code.</param>
    /// <returns>List of high score entries for the event.</returns>
    /// <response code="200">Returns the high scores for the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{eventCode}")]
    [RedisCache("frc:highscores", 5)]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScoresForEvent(int year, string eventCode)
    {
        if (eventCode.Equals("offline", StringComparison.CurrentCultureIgnoreCase))
            // If using an offline event, sometimes they do have internet, and we make a request for high
            // scores anyway. In the case that gets through return an empty list instead of 404.
            return Ok(new List<HighScore>());

        var events = await frcApi.Get<EventListResponse>($"{year}/events?eventCode={eventCode}");
        if (events?.EventCount < 1) return NotFound("Event not found");
        var qualMatches = await schedule.BuildHybridSchedule($"{year}", eventCode, "qual");
        var playoffMatches = await schedule.BuildHybridSchedule($"{year}", eventCode, "playoff");
        var allMatches = qualMatches?.Schedule ?? [];
        allMatches.AddRange(playoffMatches?.Schedule ?? []);
        // TODO: find a better way to filter these demo teams out, this way is not sustainable
        var matches = allMatches.Select(m =>
        {
            m.DistrictCode = events?.Events?[0].DistrictCode;
            m.EventCode = eventCode;
            return m;
        }).Where(m =>
            !string.IsNullOrWhiteSpace(m.PostResultTime) && !m.Teams.Any(t => t.TeamNumber is >= 9986 and <= 9999));
        var highScores = matches.CalculateHighScores(year);
        return Ok(highScores);
    }
}