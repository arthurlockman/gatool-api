using System.Net;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using System.Text.Json;
using GAToolAPI.Attributes;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using StackExchange.Redis;

namespace GAToolAPI.Controllers;

[Route("ftc/v2/{year}")]
public class FtcApiController(
    FTCApiService ftcApi,
    FTCScoutApiService ftcScoutApi,
    TeamDataService teamDataService,
    IConnectionMultiplexer connectionMultiplexer)
    : ControllerBase
{
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();

    [HttpGet("teams")]
    [RedisCache("ftcapi:teams", RedisCacheTime.FiveMinutes)]
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

    [HttpGet("team/{teamNumber:int}/awards")]
    [OpenApiTag("FTC Team Data")]
    [ProducesResponseType(typeof(Dictionary<string, TeamAwardsResponse?>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamAwardsData(int year, int teamNumber)
    {
        var result = await GetLast3YearAwards(year, teamNumber);
        return Ok(result);
    }

    [HttpPost("queryAwards")]
    [RedisCache("ftcapi:batch-team-awards", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FTC Team Data")]
    [ProducesResponseType(typeof(Dictionary<int, Dictionary<string, TeamAwardsResponse?>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> QueryAwards(int year, [FromBody] TeamQueryRequest request)
    {
        if (request.Teams.Count == 0) return BadRequest("Teams list is required");

        var awards = new ConcurrentDictionary<int, object?>();

        var tasks = request.Teams.Select(async team =>
        {
            var teamAwards = await GetLast3YearAwards(year, team);
            awards[team] = teamAwards;
        }).ToArray();

        await Task.WhenAll(tasks);

        return Ok(awards.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    /// <summary>
    /// Gets quick statistics for a specific FTC team from FTC Scout
    /// </summary>
    /// <param name="year">The competition year/season</param>
    /// <param name="teamNumber">The FTC team number</param>
    /// <returns>Quick statistics data from FTC Scout API</returns>
    /// <response code="200">Returns the team's quick statistics</response>
    /// <response code="204">No data found for the specified team and year</response>
    [HttpGet("ftcscout/quick-stats/{teamNumber}")]
    [RedisCache("ftcscout:quick-stats", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FTC Scout Team Data")]
    [ProducesResponseType(typeof(FtcScoutQuickStats), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetFtcScoutQuickStats(string year, string teamNumber)
    {
        var result = await ftcScoutApi.Get<FtcScoutQuickStats>($"teams/{teamNumber}/quick-stats?season={year}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    /// <summary>
    /// Gets events data for a specific FTC team from FTC Scout
    /// </summary>
    /// <param name="year">The competition year/season</param>
    /// <param name="teamNumber">The FTC team number</param>
    /// <returns>Events data from FTC Scout API</returns>
    /// <response code="200">Returns the team's events data</response>
    /// <response code="204">No data found for the specified team and year</response>
    [HttpGet("ftcscout/events/{teamNumber}")]
    [RedisCache("ftcscout:events", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FTC Scout Team Data")]
    [ProducesResponseType(typeof(List<FtcScoutEventData>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetFtcScoutEvents(string year, string teamNumber)
    {
        var result = await ftcScoutApi.Get<List<FtcScoutEventData>>($"teams/{teamNumber}/events/{year}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    #region Private Methods

    private async Task<TeamAwardsResponse?> GetTeamAwards(int season, int team)
    {
        var cacheKey = $"ftc:team:{team}:season:{season}:awards";

        var cachedResult = await _redis.StringGetAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedResult)) return JsonSerializer.Deserialize<TeamAwardsResponse?>(cachedResult!);

        try
        {
            var result = await ftcApi.Get<TeamAwardsResponse>($"{season}/awards/{team}");
            var cachePeriod = DateTime.Now.Year == season ? TimeSpan.FromMinutes(5) : TimeSpan.FromDays(14);
            await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), cachePeriod);
            return result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, TeamAwardsResponse?>> GetLast3YearAwards(int year, int team)
    {
        var currentYearAwards = await GetTeamAwards(year, team);
        var pastYearAwards = await GetTeamAwards(year - 1, team);
        var secondYearAwards = await GetTeamAwards(year - 2, team);

        return new Dictionary<string, TeamAwardsResponse?>
        {
            [year.ToString()] = currentYearAwards,
            [(year - 1).ToString()] = pastYearAwards,
            [(year - 2).ToString()] = secondYearAwards
        };
    }

    #endregion
}