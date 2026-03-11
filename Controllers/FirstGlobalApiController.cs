using System.Net;
using GAToolAPI.Attributes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

/// <summary>
///     Proxy for the FIRST Global Challenge API (https://api.first.global/v1).
///     Year is required in the path. The current season starts October 1; for the current season
///     the API is called without a year parameter; for prior seasons the API is called with ?year=YYYY.
/// </summary>
/// <remarks>
///     Tournament levels: t2 = RankLevel, t3 = RoundRobinLevel, t4 = FinalsLevel (see /tournaments for mappings).
///     Alliances are only available for t3 and t4.
/// </remarks>
[ApiController]
[Route("v3/firstglobal")]
[OpenApiTag("FIRST Global")]
public class FirstGlobalApiController(ILogger<FirstGlobalApiController> logger, FirstGlobalApiService firstGlobalApi)
    : ControllerBase
{
    /// <summary>
    ///     Season year that is considered "current" (season starts October 1 of that year).
    /// </summary>
    private static int CurrentSeasonYear
    {
        get
        {
            var now = DateTime.UtcNow;
            return now.Month >= 10 ? now.Year : now.Year - 1;
        }
    }

    /// <summary>
    ///     Builds query params for the external API. For the current season, no year is sent.
    ///     For prior seasons, adds year=YYYY so the API returns that season's data.
    /// </summary>
    private static Dictionary<string, string?>? YearQuery(string year)
    {
        if (int.TryParse(year, out var y) && y == CurrentSeasonYear)
            return null;
        return new Dictionary<string, string?> { ["year"] = year };
    }

    /// <summary>
    ///     Merges optional year query with additional query parameters (e.g. tournamentKey).
    /// </summary>
    private static Dictionary<string, string?>? MergeQuery(Dictionary<string, string?>? yearQuery,
        Dictionary<string, string?>? other)
    {
        if (yearQuery == null && other == null) return null;
        if (yearQuery == null) return other;
        if (other == null) return yearQuery;
        var merged = new Dictionary<string, string?>(yearQuery);
        foreach (var kv in other) merged[kv.Key] = kv.Value;
        return merged;
    }

    /// <summary>
    ///     Returns all FIRST Global data compiled into one object (teams, matches, rankings, alliances, tournaments, fieldsets).
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>Combined object with teams, matches, rankings, alliances, tournaments, and fieldsets for the season.</returns>
    /// <response code="200">Returns the combined data object.</response>
    /// <response code="204">No data available for the season.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetAll(string year)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<object>("", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global all data for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     All teams at the FIRST Global event.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>Array of team objects (teamKey, country, name, etc.).</returns>
    /// <response code="200">Returns the team list.</response>
    /// <response code="204">No teams found for the season.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/teams")]
    [RedisCache("firstglobal:teams", RedisCacheTime.OneHour)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeams(string year)
    {
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<object>("teams", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global teams for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     All matches for all tournaments.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>Array of match objects across all tournament levels.</returns>
    /// <response code="200">Returns the match list.</response>
    /// <response code="204">No matches found for the season.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/matches")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetMatches(string year)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<object>("matches", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global matches for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     All matches for a given tournament level (t2 = RankLevel, t3 = RoundRobinLevel, t4 = FinalsLevel).
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <param name="tournamentKey">Tournament level: t2, t3, or t4.</param>
    /// <returns>Array of match objects for the specified tournament level.</returns>
    /// <response code="200">Returns the match list for the tournament level.</response>
    /// <response code="204">No matches found.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/matches/{tournamentKey:regex(^t[[234]]$)}")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetMatchesByTournament(string year, string tournamentKey)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var query = MergeQuery(YearQuery(year), new Dictionary<string, string?> { ["tournamentKey"] = tournamentKey });
            var result = await firstGlobalApi.Get<object>("matches", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global matches for year {Year}, tournamentKey {TournamentKey}",
                year, tournamentKey);
            return NoContent();
        }
    }

    /// <summary>
    ///     All rankings for all tournaments.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>Array of ranking entries across all tournament levels.</returns>
    /// <response code="200">Returns the rankings list.</response>
    /// <response code="204">No rankings found for the season.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/rankings")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetRankings(string year)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<object>("rankings", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global rankings for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     Rankings for a given tournament level (t2 = RankLevel, t3 = RoundRobinLevel, t4 = FinalsLevel).
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <param name="tournamentKey">Tournament level: t2, t3, or t4.</param>
    /// <returns>Array of ranking entries for the specified tournament level.</returns>
    /// <response code="200">Returns the rankings for the tournament level.</response>
    /// <response code="204">No rankings found.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/rankings/{tournamentKey:regex(^t[[234]]$)}")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetRankingsByTournament(string year, string tournamentKey)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var query = MergeQuery(YearQuery(year), new Dictionary<string, string?> { ["tournamentKey"] = tournamentKey });
            var result = await firstGlobalApi.Get<object>("rankings", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global rankings for year {Year}, tournamentKey {TournamentKey}",
                year, tournamentKey);
            return NoContent();
        }
    }

    /// <summary>
    ///     All alliances for a given tournament level. Only available for RoundRobinLevel (t3) and FinalsLevel (t4).
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <param name="tournamentKey">Tournament level: t3 (RoundRobin) or t4 (Finals).</param>
    /// <returns>Array of alliance objects (captain, picks, rankingScore, etc.).</returns>
    /// <response code="200">Returns the alliance list for the tournament level.</response>
    /// <response code="204">No alliances found.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/alliances/{tournamentKey:regex(^t[[34]]$)}")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetAlliances(string year, string tournamentKey)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var query = MergeQuery(YearQuery(year), new Dictionary<string, string?> { ["tournamentKey"] = tournamentKey });
            var result = await firstGlobalApi.Get<object>("alliances", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global alliances for year {Year}, tournamentKey {TournamentKey}",
                year, tournamentKey);
            return NoContent();
        }
    }

    /// <summary>
    ///     Tournament to tournamentKey mappings (e.g. RankLevel -> t2, RoundRobinLevel -> t3, FinalsLevel -> t4).
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>Object mapping level names (RankLevel, RoundRobinLevel, FinalsLevel) to keys (t2, t3, t4).</returns>
    /// <response code="200">Returns the tournament key mappings.</response>
    /// <response code="204">No tournament data for the season.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/tournaments")]
    [RedisCache("firstglobal:tournaments", RedisCacheTime.OneDay)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTournaments(string year)
    {
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<object>("tournaments", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global tournaments for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     Field groupings (2D array of field indices).
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>2D array of integers representing field groupings.</returns>
    /// <response code="200">Returns the fieldset groupings.</response>
    /// <response code="204">No fieldsets for the season.</response>
    [HttpGet("{year:regex(^\\d{{4}}$)}/fieldsets")]
    [RedisCache("firstglobal:fieldsets", RedisCacheTime.OneDay)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetFieldsets(string year)
    {
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<object>("fieldsets", query);
            if (result == null) return NoContent();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global fieldsets for year {Year}", year);
            return NoContent();
        }
    }
}
