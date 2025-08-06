using System.Net;
using System.Text.Json.Nodes;
using GAToolAPI.Attributes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[Route("ftc/v2/{year}")]
public class FtcApiController(FTCApiService ftcApi, TOAApiService toaApi, TeamDataService teamDataService)
    : ControllerBase
{
    [HttpGet("teams")]
    [RedisCache("ftcapi:teams", RedisCacheTime.OneWeek)]
    [OpenApiTag("FTC Team Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeams(string year, [FromQuery] string? eventCode,
        [FromQuery] string? state, [FromQuery] int? teamNumber)
    {
        var result = await teamDataService.GetFtcTeamData(year, eventCode, teamNumber, state);
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("schedule/{eventCode}/{tournamentLevel}")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEventSchedule(string year, string eventCode, string tournamentLevel)
    {
        var result = await ftcApi.GetGeneric($"{year}/schedule/{eventCode}?tournamentLevel={tournamentLevel}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("awards/event/{eventCode}")]
    [RedisCache("ftcapi:awards", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEventAwards(string year, string eventCode)
    {
        var result = await ftcApi.GetGeneric($"{year}/awards/{eventCode}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("events")]
    [RedisCache("ftcapi:events", RedisCacheTime.OneDay)]
    [OpenApiTag("FTC Season Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEvents(string year)
    {
        var result = await ftcApi.GetGeneric($"{year}/events");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("events/{eventCode}")]
    [RedisCache("ftcapi:event", RedisCacheTime.OneDay)]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEvent(string year, string eventCode)
    {
        var result = await ftcApi.GetGeneric($"{year}/events?eventCode={eventCode}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("scores/{eventCode}/{tournamentLevel}/{start}/{end}")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetScores(string year, string eventCode, string tournamentLevel, string start,
        string end)
    {
        JsonObject? result;
        if (start == end)
            result = await ftcApi.GetGeneric($"{year}/scores/{eventCode}/{tournamentLevel}?matchNumber={start}");
        else
            result = await ftcApi.GetGeneric($"{year}/scores/{eventCode}/{tournamentLevel}?start={start}&end={end}");

        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("scores/{eventCode}/playoff")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetPlayoffScores(string year, string eventCode)
    {
        var result = await ftcApi.GetGeneric($"{year}/scores/{eventCode}/Playoff");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("scores/{eventCode}/qual")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetQualScores(string year, string eventCode)
    {
        var result = await ftcApi.GetGeneric($"{year}/scores/{eventCode}/Qual");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("leagues")]
    [OpenApiTag("FTC League Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetLeagues(string year)
    {
        var result = await ftcApi.GetGeneric($"{year}/leagues");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("leagues/rankings/{regionCode}/{leagueCode}")]
    [OpenApiTag("FTC League Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetLeagueRankings(string year, string regionCode, string leagueCode)
    {
        var result = await ftcApi.GetGeneric($"{year}/leagues/rankings/{regionCode}/{leagueCode}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("matches/{eventCode}/{tournamentLevel}")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetMatches(string year, string eventCode, string tournamentLevel)
    {
        var result = await ftcApi.GetGeneric($"{year}/matches/{eventCode}?tournamentLevel={tournamentLevel}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("schedule/hybrid/{eventCode}/{tournamentLevel}")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetHybridSchedule(string year, string eventCode, string tournamentLevel)
    {
        var result = await ftcApi.GetGeneric($"{year}/schedule/{eventCode}/{tournamentLevel}/hybrid");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("rankings/{eventCode}")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetRankings(string year, string eventCode)
    {
        var rankings = await ftcApi.GetGeneric($"{year}/rankings/{eventCode}");
        if (rankings == null) return NoContent();

        var result = new JsonObject
        {
            ["rankings"] = rankings
        };
        return Ok(result);
    }

    [HttpGet("alliances/{eventCode}")]
    [OpenApiTag("FTC Event Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetAlliances(string year, string eventCode)
    {
        var result = await ftcApi.GetGeneric($"{year}/alliances/{eventCode}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("awards/team/{teamNumber}")]
    [RedisCache("ftcapi:team-awards", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FTC Team Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamAwards(string year, string teamNumber)
    {
        var result = await ftcApi.GetGeneric($"{year}/awards/{teamNumber}");
        if (result == null) return NoContent();
        return Ok(result);
    }
}