using System.Net;
using GAToolAPI.Attributes;
using GAToolAPI.Helpers;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

/// <summary>
///     Proxy for the FIRST Global Challenge API (https://api.first.global/v1).
///     Year is required in the path. The current season starts October 1; for the current season
///     the API is called without a year parameter; for prior seasons the API is called with ?year=YYYY.
///     Responses are converted to FRC-compatible shapes (teams, matches, rankings, alliances).
/// </summary>
/// <remarks>
///     Tournament levels: t2 = Qualification, t3 = Playoff (Round Robin), t4 = Finals.
///     In t2: 3 teams per alliance (stations 11-13 = Red, 21-23 = Blue).
///     In t3/t4: 4 teams per alliance (stations 11-14 = Red, 21-24 = Blue).
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
    ///     Translates public-facing tournament level names to FIRST Global API keys.
    ///     Accepts either the raw key (t2/t3/t4) or the FRC-style aliases (qual/playoff/final).
    /// </summary>
    private static string? NormalizeTournamentKey(string tournamentKey) => tournamentKey.ToLowerInvariant() switch
    {
        "qual" => "t2",
        "playoff" or "playoffs" => "t3",
        "final" or "finals" => "t4",
        "t2" or "t3" or "t4" => tournamentKey.ToLowerInvariant(),
        _ => null
    };

    private static string? NormalizeAllianceTournamentKey(string tournamentKey) => tournamentKey.ToLowerInvariant() switch
    {
        "playoff" or "playoffs" => "t3",
        "final" or "finals" => "t4",
        "t3" or "t4" => tournamentKey.ToLowerInvariant(),
        _ => null
    };

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
    [HttpGet("{year:int}")]
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
    ///     All teams at the FIRST Global event, converted to FRC team format.
    ///     Each team's <c>teamKey</c> maps to <c>TeamNumber</c> and the country name to <c>NameFull</c>/<c>Country</c>.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>FRC-format teams response with team list and counts.</returns>
    /// <response code="200">Returns the team list.</response>
    /// <response code="204">No teams found for the season.</response>
    [HttpGet("{year:int}/teams")]
    [RedisCache("firstglobal:teams", RedisCacheTime.OneHour)]
    [ProducesResponseType(typeof(FgTeamsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeams(string year)
    {
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<List<FgTeam>>("teams", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFrcTeams(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global teams for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     All matches for all tournaments, converted to FRC match format.
    ///     Stations are mapped to FRC-style strings (Red1/Blue1 etc.) and tournament keys to level names.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>FRC-format matches response.</returns>
    /// <response code="200">Returns the match list.</response>
    /// <response code="204">No matches found for the season.</response>
    [HttpGet("{year:int}/matches")]
    [RedisCache("firstglobal:matches", RedisCacheTime.FiveMinutes)]
    [ProducesResponseType(typeof(FgMatchesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetMatches(string year)
    {
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<List<FgMatch>>("matches", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFrcMatches(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global matches for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     All matches for a given tournament level, converted to FRC match format.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <param name="tournamentKey">Tournament level: t2 (Qualification), t3 (Playoff), or t4 (Finals).</param>
    /// <returns>FRC-format matches response for the specified tournament level.</returns>
    /// <response code="200">Returns the match list for the tournament level.</response>
    /// <response code="204">No matches found.</response>
    [HttpGet("{year:int}/matches/{tournamentKey}")]
    [RedisCache("firstglobal:matches", RedisCacheTime.FiveMinutes)]
    [ProducesResponseType(typeof(FgMatchesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetMatchesByTournament(string year, string tournamentKey)
    {
        try
        {
            var key = NormalizeTournamentKey(tournamentKey);
            if (key == null) return NotFound();
            var query = MergeQuery(YearQuery(year), new Dictionary<string, string?> { ["tournamentKey"] = key });
            var result = await firstGlobalApi.Get<List<FgMatch>>("matches", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFrcMatches(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global matches for year {Year}, tournamentKey {TournamentKey}",
                year, tournamentKey);
            return NoContent();
        }
    }

    /// <summary>
    ///     Score breakdowns for all matches across all tournament levels.
    ///     Extracts game-specific details (biodiversity, barriers, parking, etc.) from each match.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>FRC-style MatchScores response with per-alliance game detail breakdowns.</returns>
    /// <response code="200">Returns the match scores.</response>
    /// <response code="204">No scores found for the season.</response>
    [HttpGet("{year:int}/scores")]
    [ProducesResponseType(typeof(FgMatchScoresResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetScores(string year)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<List<FgMatch>>("matches", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFgScores(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global scores for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     Score breakdowns for a given tournament level.
    ///     Accepts t2/qual, t3/playoff, t4/final.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <param name="tournamentKey">Tournament level: t2/qual, t3/playoff, or t4/final.</param>
    /// <returns>FRC-style MatchScores response for the specified level.</returns>
    /// <response code="200">Returns the match scores for the tournament level.</response>
    /// <response code="204">No scores found.</response>
    [HttpGet("{year:int}/scores/{tournamentKey}")]
    [ProducesResponseType(typeof(FgMatchScoresResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetScoresByTournament(string year, string tournamentKey)
    {
        Response.Headers.CacheControl = "no-cache";
        try
        {
            var key = NormalizeTournamentKey(tournamentKey);
            if (key == null) return NotFound();
            var query = MergeQuery(YearQuery(year), new Dictionary<string, string?> { ["tournamentKey"] = key });
            var result = await firstGlobalApi.Get<List<FgMatch>>("matches", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFgScores(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global scores for year {Year}, tournamentKey {TournamentKey}",
                year, tournamentKey);
            return NoContent();
        }
    }

    /// <summary>
    ///     All rankings for all tournaments, converted to FRC rankings format.
    ///     SortOrder1 = ranking score, SortOrder2 = highest score, SortOrder3 = protection points.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>FRC-format rankings response.</returns>
    /// <response code="200">Returns the rankings.</response>
    /// <response code="204">No rankings found for the season.</response>
    [HttpGet("{year:int}/rankings")]
    [RedisCache("firstglobal:rankings", RedisCacheTime.FiveMinutes)]
    [ProducesResponseType(typeof(RankingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetRankings(string year)
    {
        try
        {
            var query = YearQuery(year);
            var result = await firstGlobalApi.Get<List<FgRanking>>("rankings", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFrcRankings(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global rankings for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     Rankings for a given tournament level, converted to FRC rankings format.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <param name="tournamentKey">Tournament level: t2, t3, or t4.</param>
    /// <returns>FRC-format rankings response for the specified tournament level.</returns>
    /// <response code="200">Returns the rankings for the tournament level.</response>
    /// <response code="204">No rankings found.</response>
    [HttpGet("{year:int}/rankings/{tournamentKey}")]
    [RedisCache("firstglobal:rankings", RedisCacheTime.FiveMinutes)]
    [ProducesResponseType(typeof(RankingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetRankingsByTournament(string year, string tournamentKey)
    {
        try
        {
            var key = NormalizeTournamentKey(tournamentKey);
            if (key == null) return NotFound();
            var query = MergeQuery(YearQuery(year), new Dictionary<string, string?> { ["tournamentKey"] = key });
            var result = await firstGlobalApi.Get<List<FgRanking>>("rankings", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFrcRankings(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global rankings for year {Year}, tournamentKey {TournamentKey}",
                year, tournamentKey);
            return NoContent();
        }
    }

    /// <summary>
    ///     All alliances for a given tournament level, converted to FRC alliance format.
    ///     Only available for Playoff (t3) and Finals (t4).
    ///     Captain and picks map to FRC Captain/Round1/Round2/Round3 fields.
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <param name="tournamentKey">Tournament level: t3 (Playoff) or t4 (Finals).</param>
    /// <returns>FRC-format alliances response.</returns>
    /// <response code="200">Returns the alliance list for the tournament level.</response>
    /// <response code="204">No alliances found.</response>
    [HttpGet("{year:int}/alliances/{tournamentKey}")]
    [RedisCache("firstglobal:alliances", RedisCacheTime.FiveMinutes)]
    [ProducesResponseType(typeof(AlliancesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetAlliances(string year, string tournamentKey)
    {
        try
        {
            var key = NormalizeAllianceTournamentKey(tournamentKey);
            if (key == null) return NotFound();
            var query = MergeQuery(YearQuery(year), new Dictionary<string, string?> { ["tournamentKey"] = key });
            var result = await firstGlobalApi.Get<List<FgAlliance>>("alliances", query);
            if (result == null) return NoContent();
            return Ok(FirstGlobalConverter.ToFrcAlliances(result));
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
    [HttpGet("{year:int}/tournaments")]
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
    [HttpGet("{year:int}/fieldsets")]
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
