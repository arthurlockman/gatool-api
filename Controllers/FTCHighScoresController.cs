using System.Net;
using GAToolAPI.Attributes;
using GAToolAPI.Extensions;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[Route("ftc/v2/{year:int}/highscores")]
[OpenApiTag("FTC High Scores")]
// ReSharper disable once InconsistentNaming
public class FTCHighScoresController(
    FTCApiService ftcApi,
    FTCScheduleService ftcSchedule,
    UserStorageService storageService)
    : ControllerBase
{
    /// <summary>
    ///     Gets all FTC high scores for the season across all events.
    /// </summary>
    /// <param name="year">The competition year/season.</param>
    /// <returns>List of high score entries.</returns>
    /// <response code="200">Returns the high scores list.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScores(int year)
    {
        var scores = await storageService.GetHighScores(year, "FTC-");
        return Ok(scores);
    }

    /// <summary>
    ///     Gets FTC high scores for a specific league (region + league code).
    /// </summary>
    /// <param name="year">The competition year/season.</param>
    /// <param name="regionCode">The region code.</param>
    /// <param name="leagueCode">The league code.</param>
    /// <returns>List of high score entries for the league.</returns>
    /// <response code="200">Returns the high scores for the league.</response>
    [HttpGet("league/{regionCode}/{leagueCode}")]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScoresForLeague(int year, string regionCode, string leagueCode)
    {
        var leaguePrefix = $"FTCLeague{regionCode}{leagueCode}";
        var scores = await storageService.GetHighScores(year, leaguePrefix);
        return Ok(scores);
    }

    /// <summary>
    ///     Gets FTC high scores for a specific region.
    /// </summary>
    /// <param name="year">The competition year/season.</param>
    /// <param name="regionCode">The region code.</param>
    /// <returns>List of high score entries for the region.</returns>
    /// <response code="200">Returns the high scores for the region.</response>
    [HttpGet("region/{regionCode}")]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScoresForRegion(int year, string regionCode)
    {
        var regionPrefix = $"FTCRegion{regionCode}";
        var scores = await storageService.GetHighScores(year, regionPrefix);
        return Ok(scores);
    }

    /// <summary>
    ///     Gets calculated high scores for a specific FTC event.
    /// </summary>
    /// <param name="year">The competition year/season.</param>
    /// <param name="eventCode">The event code.</param>
    /// <returns>List of high score entries for the event.</returns>
    /// <response code="200">Returns the high scores for the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{eventCode}")]
    [RedisCache("ftc:highscores", 5)]
    [ProducesResponseType(typeof(List<HighScore>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetHighScoresForEvent(int year, string eventCode)
    {
        if (eventCode.Equals("offline", StringComparison.CurrentCultureIgnoreCase)) return Ok(new List<HighScore>());

        var events = await ftcApi.Get<FTCEventListResponse>($"{year}/events?eventCode={eventCode}");
        if (events?.Events == null || events.Events.Count < 1) return NotFound("Event not found");

        var qualMatches = await ftcSchedule.BuildHybridSchedule($"{year}", eventCode, "qual");
        var playoffMatches = await ftcSchedule.BuildHybridSchedule($"{year}", eventCode, "playoff");
        var allMatches = qualMatches?.Schedule ?? [];
        allMatches.AddRange(playoffMatches?.Schedule ?? []);

        var matches = allMatches.Select(m =>
        {
            m.DistrictCode = events.Events[0].RegionCode != null
                ? events.Events[0].LeagueCode != null
                    ? $"{events.Events[0].RegionCode}-{events.Events[0].LeagueCode}"
                    : events.Events[0].RegionCode
                : null;
            m.EventCode = eventCode;
            return m;
        }).Where(m => !string.IsNullOrWhiteSpace(m.PostResultTime));

        var highScores = matches.CalculateHighScores(year, "FTC");
        return Ok(highScores);
    }
}