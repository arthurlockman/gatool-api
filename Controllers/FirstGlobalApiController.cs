using System.Net;
using GAToolAPI.Attributes;
using GAToolAPI.Helpers;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

/// <summary>
///     Proxy for the FIRST Global Challenge API (https://api.first.global/v1).
///     Year is required in the path and is always passed through to the remote API as ?year=YYYY.
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
    ///     Builds the year query param for the external API. Always sent as ?year=YYYY so the
    ///     remote API returns data for the requested season explicitly.
    /// </summary>
    private static Dictionary<string, string?> YearQuery(string year) =>
        new() { ["year"] = year };

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
    ///     All awards for the season, converted to FRC award format.
    ///     Each gold/silver/bronze/other recipient becomes its own award row; <c>TeamNumber</c> is
    ///     resolved from the recipient's country against the season's teams.
    ///     Medal tier is encoded in <c>Series</c>: 1 = gold, 2 = silver, 3 = bronze. An <c>other</c>
    ///     recipient inherits the tier of its <c>class</c> field (e.g. a tied gold co-medalist also
    ///     gets <c>Series = 1</c>); if <c>class</c> is absent, <c>Series</c> is <c>null</c>, meaning the
    ///     award has no gold/silver/bronze ranking (e.g. the Safety Award).
    /// </summary>
    /// <param name="year">Season year (e.g. 2025). Required.</param>
    /// <returns>FRC-format event awards response.</returns>
    /// <response code="200">Returns the award list.</response>
    /// <response code="204">No awards found for the season.</response>
    [HttpGet("{year:int}/awards")]
    [RedisCache("firstglobal:awards", RedisCacheTime.FiveMinutes)]
    [ProducesResponseType(typeof(EventAwardsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetAwards(string year)
    {
        try
        {
            var query = YearQuery(year);
            var awardsTask = firstGlobalApi.Get<List<FgAward>>("awards", query);
            var teamsTask = firstGlobalApi.Get<List<FgTeam>>("teams", query);
            var awards = await awardsTask;
            if (awards == null) return NoContent();
            var teams = await teamsTask ?? [];
            return Ok(FirstGlobalConverter.ToFrcAwards(awards, teams));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global awards for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    ///     Gets FIRST Global awards for the requested season and the two preceding seasons, grouped
    ///     by stable two-character <c>countryCode</c> instead of the season-specific team number.
    ///     FIRST Global also uses numeric codes for some teams (for example, <c>10</c> for Hope
    ///     (Refugees) and <c>15</c> for Chinese Taipei).
    ///     Historical award rows use the country's team number from the requested season.
    /// </summary>
    /// <param name="year">The requested competition year/season.</param>
    /// <param name="request">
    ///     Optional team filter containing two-character <c>countryCode</c> values. Omit the body,
    ///     use an empty body, or provide an empty <c>teams</c> array to return every country code
    ///     represented in the three seasons.
    /// </param>
    /// <returns>Dictionary of country code to dictionary of year to awards.</returns>
    /// <response code="200">Returns three seasons of awards grouped by country.</response>
    /// <response code="400">One or more country codes are not two alphanumeric characters.</response>
    [HttpPost("{year:int}/queryAwards")]
    [RedisCache("firstglobal:batch-country-awards", RedisCacheTime.FiveMinutes)]
    [ProducesResponseType(typeof(Dictionary<string, Dictionary<string, TeamAwardsResponse>>),
        (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> QueryAwards(int year,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FgAwardsQueryRequest? request)
    {
        var requestedCountryCodes = request?.Teams?
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (requestedCountryCodes.Any(code => code.Length != 2 || !code.All(char.IsLetterOrDigit)))
            return BadRequest("Teams values must be two-character alphanumeric countryCode values");

        try
        {
            var years = new[] { year, year - 1, year - 2 };
            var awardsTasks = years.ToDictionary(
                season => season,
                season => firstGlobalApi.Get<List<FgAward>>("awards", YearQuery(season.ToString())));
            var requestedYearTeamsTask = firstGlobalApi.Get<List<FgTeam>>("teams", YearQuery(year.ToString()));

            await Task.WhenAll(awardsTasks.Values.Cast<Task>().Append(requestedYearTeamsTask));

            var awardsByYear = awardsTasks.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Result ?? []);
            var countryCodes = requestedCountryCodes.Count > 0
                ? requestedCountryCodes
                : awardsByYear.Values
                    .SelectMany(FirstGlobalConverter.AwardCountryCodes)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.Ordinal)
                    .ToList();
            var requestedYearTeams = requestedYearTeamsTask.Result ?? [];
            var groupedByYear = awardsByYear.ToDictionary(
                pair => pair.Key,
                pair => FirstGlobalConverter.ToFrcAwardsByCountryCode(pair.Value, requestedYearTeams, countryCodes));

            var response = countryCodes.ToDictionary(
                countryCode => countryCode,
                countryCode => years.ToDictionary(
                    season => season.ToString(),
                    season => groupedByYear[season][countryCode]),
                StringComparer.OrdinalIgnoreCase);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching FIRST Global awards for year {Year} and prior seasons", year);
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
