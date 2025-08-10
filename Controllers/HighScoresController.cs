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
public class HighScoresController(FRCApiService frcApi, ScheduleService schedule, UserStorageService storageService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScores(int year)
    {
        var scores = await storageService.GetHighScores(year);
        return Ok(scores);
    }

    [HttpGet("{eventCode}")]
    [RedisCache("frc:highscores", 5)]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScoresForEvent(int year, string eventCode)
    {
        if (eventCode.Equals("offline", StringComparison.CurrentCultureIgnoreCase))
        {
            // If using an offline event, sometimes they do have internet, and we make a request for high
            // scores anyway. In the case that gets through return an empty list instead of 404.
            return Ok(new List<HighScore>());
        }

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