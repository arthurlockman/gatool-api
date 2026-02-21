using System.Net;
using System.Text.Json.Nodes;
using GAToolAPI.Attributes;
using GAToolAPI.Extensions;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[Route("ftc/v2/{year:int}/highscores")]
[OpenApiTag("FTC High Scores")]
public class FTCHighScoresController(
    FTCApiService ftcApi,
    FTCScheduleService ftcSchedule,
    UserStorageService storageService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScores(int year)
    {
        var scores = await storageService.GetHighScores(year, "FTC-");
        return Ok(scores);
    }

    [HttpGet("league/{regionCode}/{leagueCode}")]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScoresForLeague(int year, string regionCode, string leagueCode)
    {
        var leaguePrefix = $"FTCLeague{regionCode}{leagueCode}";
        var scores = await storageService.GetHighScores(year, leaguePrefix);
        return Ok(scores);
    }

    [HttpGet("{eventCode}")]
    [RedisCache("ftc:highscores", 5)]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScoresForEvent(int year, string eventCode)
    {
        if (eventCode.Equals("offline", StringComparison.CurrentCultureIgnoreCase))
        {
            return Ok(new List<HighScore>());
        }

        var events = await ftcApi.Get<FTCEventListResponse>($"{year}/events?eventCode={eventCode}");
        if (events?.Events == null || events.Events.Count < 1) return NotFound("Event not found");

        var qualMatches = await ftcSchedule.BuildHybridSchedule($"{year}", eventCode, "qual");
        var playoffMatches = await ftcSchedule.BuildHybridSchedule($"{year}", eventCode, "playoff");
        var allMatches = qualMatches?.Schedule ?? [];
        allMatches.AddRange(playoffMatches?.Schedule ?? []);

        var matches = allMatches.Select(m =>
        {
            m.DistrictCode = events.Events[0].LeagueCode != null && events.Events[0].RegionCode != null
                ? $"{events.Events[0].RegionCode}-{events.Events[0].LeagueCode}"
                : null;
            m.EventCode = eventCode;
            return m;
        }).Where(m => !string.IsNullOrWhiteSpace(m.PostResultTime));

        var highScores = matches.CalculateHighScores(year, "FTC");
        return Ok(highScores);
    }
}
