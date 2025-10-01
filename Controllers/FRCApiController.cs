using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using GAToolAPI.Attributes;
using GAToolAPI.Extensions;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using StackExchange.Redis;

namespace GAToolAPI.Controllers;

[ApiController]
[Route("v3/{year}")]
public class FrcApiController(
    ILogger<FrcApiController> logger,
    FRCApiService frcApiClient,
    TBAApiService tbaApiClient,
    StatboticsApiService statboticsApiClient,
    IConnectionMultiplexer connectionMultiplexer,
    TeamDataService teamDataService,
    ScheduleService scheduleService)
    : ControllerBase
{
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();

    [HttpGet("teams")]
    [RedisCache("frcapi:teams", RedisCacheTime.OneWeek)]
    [OpenApiTag("FRC Team Data")]
    [ProducesResponseType(typeof(TeamsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeams(string year, [FromQuery] string? eventCode,
        [FromQuery] string? districtCode, [FromQuery] string? teamNumber)
    {
        var result = await teamDataService.GetFrcTeamData(year, eventCode, districtCode, teamNumber);
        if (result == null) return NoContent();
        if (result.Teams?.Count == 0) RedisCache.IgnoreCurrentRequest();
        return Ok(result);
    }

    [HttpGet("schedule/{eventCode}/{tournamentLevel}")]
    [RedisCache("frcapi:schedule", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Schedules and Results")]
    [ProducesResponseType(typeof(ScheduleResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetSchedule(string year, string eventCode, string tournamentLevel)
    {
        var result = await frcApiClient.Get<ScheduleResponse?>($"{year}/schedule/{eventCode}/{tournamentLevel}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("districts")]
    [RedisCache("frcapi:districtlist", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Events")]
    [ProducesResponseType(typeof(DistrictsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetDistricts(string year)
    {
        var result = await frcApiClient.Get<DistrictsResponse>($"{year}/districts");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("matches/{eventCode}/{tournamentLevel}")]
    [RedisCache("frcapi:districtlist", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Schedules and Results")]
    [ProducesResponseType(typeof(MatchesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetMatches(string year, string eventCode, string tournamentLevel)
    {
        var result = await frcApiClient.Get<MatchesResponse>($"{year}/matches/{eventCode}/{tournamentLevel}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("schedule/hybrid/{eventCode}/{tournamentLevel}")]
    [RedisCache("frcapi:schedule", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Schedules and Results")]
    [ProducesResponseType(typeof(HybridScheduleResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetHybridSchedule(string year, string eventCode, string tournamentLevel)
    {
        var result = await scheduleService.BuildHybridSchedule(year, eventCode, tournamentLevel);
        if (result == null) return NoContent();
        return Ok(new HybridScheduleResponse
        {
            Schedule = result
        });
    }

    [HttpGet("awards/event/{eventCode}")]
    [RedisCache("frcapi:events", RedisCacheTime.OneWeek)]
    [OpenApiTag("FRC Schedules and Results")]
    [ProducesResponseType(typeof(EventAwardsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEventAwards(string year, string eventCode)
    {
        var result = await frcApiClient.Get<EventAwardsResponse>($"{year}/awards/event/{eventCode}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("events")]
    [RedisCache("frcapi:events", RedisCacheTime.OneWeek)]
    [OpenApiTag("FRC Events")]
    [ProducesResponseType(typeof(EventListResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEvents(string year, [FromQuery] string? eventCode,
        [FromQuery] string? districtCode, [FromQuery] string? teamNumber)
    {
        var result = await frcApiClient.Get<EventListResponse>($"{year}/events",
            new { eventCode, districtCode, teamNumber }.ToParameterDictionary());
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("scores/{eventCode}/{tournamentLevel}/{start}/{end}")]
    [OpenApiTag("FRC Schedules and Results")]
    [ProducesResponseType(typeof(MatchScoresResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetScores(string year, string eventCode, string tournamentLevel,
        string start, string end)
    {
        Response.Headers.CacheControl = "no-cache";

        MatchScoresResponse? result;
        if (start == end)
            result = await frcApiClient.Get<MatchScoresResponse>($"{year}/scores/{eventCode}/{tournamentLevel}",
                new Dictionary<string, string?> { ["matchNumber"] = start });
        else
            result = await frcApiClient.Get<MatchScoresResponse>($"{year}/scores/{eventCode}/{tournamentLevel}",
                new { start, end }.ToParameterDictionary());

        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("scores/{eventCode}/{tournamentLevel}")]
    [OpenApiTag("FRC Schedules and Results")]
    [ProducesResponseType(typeof(MatchScoresResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetScores(string year, string eventCode, string tournamentLevel)
    {
        Response.Headers.CacheControl = "no-cache";
        var level = tournamentLevel switch
        {
            "qual" => "Qual",
            "playoff" => "Playoff",
            _ => ""
        };
        if (string.IsNullOrEmpty(level)) return BadRequest($"Unknown tournament level {tournamentLevel}");
        var result = await frcApiClient.Get<MatchScoresResponse>($"{year}/scores/{eventCode}/{level}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("team/{teamNumber:int}/awards")]
    [OpenApiTag("FRC Team Data")]
    [ProducesResponseType(typeof(Dictionary<string, TeamAwardsResponse?>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamAwardsData(int year, int teamNumber)
    {
        var result = await GetLast3YearAwards(year, teamNumber);
        return Ok(result);
    }

    [HttpPost("queryAwards")]
    [OpenApiTag("FRC Team Data")]
    [ProducesResponseType(typeof(Dictionary<int, Dictionary<string, TeamAwardsResponse?>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
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

    [HttpGet("team/{teamNumber:int}/media")]
    [RedisCache("frc:teammedia", RedisCacheTime.ThreeDays)]
    [OpenApiTag("FRC Team Data")]
    [ProducesResponseType(typeof(List<TeamMedia>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamMedia(int year, int teamNumber)
    {
        var media = await GetTeamMediaData(year, teamNumber);
        if (media == null) return NoContent();
        return Ok(media);
    }

    [HttpPost("queryMedia")]
    [RedisCache("frc:teammediaquery", RedisCacheTime.ThreeDays)]
    [OpenApiTag("FRC Team Data")]
    [ProducesResponseType(typeof(Dictionary<string, List<TeamMedia>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> QueryMedia(int year, [FromBody] TeamQueryRequest request)
    {
        if (request.Teams.Count == 0) return BadRequest("Teams list is required");

        var media = new ConcurrentDictionary<int, object?>();

        var tasks = request.Teams.Select(async team =>
        {
            var teamMedia = await GetTeamMediaData(year, team);
            media[team] = teamMedia;
        }).ToArray();

        await Task.WhenAll(tasks);

        return Ok(media.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    [HttpGet("avatars/team/{teamNumber:int}/avatar.png")]
    [RedisCache("frc:teamavatar", RedisCacheTime.OneMonth)]
    [OpenApiTag("FRC Team Data")]
    [ProducesResponseType(typeof(FileResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetTeamAvatar(int year, int teamNumber)
    {
        Response.Headers.CacheControl = "s-maxage=2629800"; // ~1 month

        try
        {
            // Fetch from FIRST API
            var avatarResponse = await frcApiClient.Get<TeamAvatarResponse>($"{year}/avatars",
                new Dictionary<string, string?> { ["teamNumber"] = teamNumber.ToString() });

            if (avatarResponse?.Teams == null || avatarResponse.Teams.Count == 0)
                return NotFound(new { message = "Avatar not found" });

            var teamAvatar = avatarResponse.Teams[0];
            if (string.IsNullOrEmpty(teamAvatar.EncodedAvatar)) return NotFound(new { message = "Avatar not found" });

            var encodedAvatar = teamAvatar.EncodedAvatar;


            // Convert base64 to byte array and return as PNG
            var avatarBytes = Convert.FromBase64String(encodedAvatar);
            return File(avatarBytes, "image/png");
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.Data.Contains("StatusCode") ? (int)ex.Data["StatusCode"]! : 404;
            return StatusCode(statusCode, new { message = "Avatar not found." });
        }
        catch (Exception)
        {
            return NotFound(new { message = "Avatar not found." });
        }
    }

    [HttpGet("rankings/{eventCode}")]
    [OpenApiTag("FRC Events")]
    [ProducesResponseType(typeof(RankingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetRankings(int year, string eventCode)
    {
        Response.Headers.CacheControl = "no-cache";

        var result = await frcApiClient.Get<RankingsData>($"{year}/rankings/{eventCode}");
        if (result == null) return NoContent();

        // Return rankings with headers structure like the TypeScript version
        return Ok(new RankingsResponse(result, null));
    }

    [HttpGet("alliances/{eventCode}")]
    [OpenApiTag("FRC Events")]
    [ProducesResponseType(typeof(AlliancesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetAlliances(int year, string eventCode)
    {
        Response.Headers.CacheControl = "no-cache";

        var result = await frcApiClient.Get<AlliancesResponse>($"{year}/alliances/{eventCode}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    [HttpGet("district/rankings/{districtCode}")]
    [RedisCache("frcapi:district:rankings", RedisCacheTime.OneDay)]
    [OpenApiTag("FRC Events")]
    [ProducesResponseType(typeof(DistrictRankingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetDistrictRankings(int year, string districtCode, [FromQuery] int? top)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["districtCode"] = districtCode,
            ["page"] = "1"
        };

        if (top.HasValue) parameters["top"] = top.Value.ToString();

        // Get first page to check if pagination is needed
        var firstPageResult = await frcApiClient.Get<DistrictRankingsResponse>($"{year}/rankings/district", parameters);
        if (firstPageResult == null) return NoContent();

        var pageTotal = firstPageResult.PageTotal;
        if (pageTotal == 1 || firstPageResult.RankingCountPage == top) return Ok(firstPageResult);

        // Fetch remaining pages in parallel
        var pageTasks = new List<Task<DistrictRankingsResponse?>>();
        for (var page = 2; page <= pageTotal; page++)
        {
            var pageParameters = new Dictionary<string, string?>(parameters)
            {
                ["page"] = page.ToString()
            };

            pageTasks.Add(Task.Run(async () =>
                await frcApiClient.Get<DistrictRankingsResponse>($"{year}/rankings/district", pageParameters)));
        }

        var pageResults = await Task.WhenAll(pageTasks);

        // Merge all pages into the first result
        var allDistrictRanks = new List<DistrictRank>(firstPageResult.DistrictRanks ?? []);
        var totalRankingCount = firstPageResult.RankingCountPage;

        // Add ranks from additional pages
        foreach (var pageResult in pageResults.Where(x => x != null))
        {
            allDistrictRanks.AddRange(pageResult!.DistrictRanks ?? []);
            totalRankingCount += pageResult.RankingCountPage;
        }

        // Create merged result
        var mergedResult = new DistrictRankingsResponse(allDistrictRanks, firstPageResult.RankingCountTotal,
            totalRankingCount, 1, 1);

        return Ok(mergedResult);
    }

    [HttpGet("offseason/teams/{eventCode}")]
    [RedisCache("tbaapi:offseason:teams", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Offseason")]
    [ProducesResponseType(typeof(OffseasonTeamsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    // ReSharper disable once UnusedParameter.Global
    public async Task<IActionResult> GetOffseasonTeams(int year, string eventCode)
    {
        Response.Headers.CacheControl = "s-maxage=600"; // 10 minutes

        try
        {
            var tbaResponse = await tbaApiClient.Get<List<TBATeam>>($"event/{eventCode}/teams");
            if (tbaResponse == null || tbaResponse.Count == 0) return NoContent();

            // Sort teams by team number
            var sortedTeams = tbaResponse.OrderBy(t => t.TeamNumber).ToList();

            var result = new List<OffseasonTeam>();

            // Transform TBA format to FIRST API format (skip index 0 like TypeScript)
            for (var i = 1; i < sortedTeams.Count; i++)
                try
                {
                    var team = sortedTeams[i];
                    var transformedTeam = new OffseasonTeam(team.TeamNumber, team.Name, team.Nickname, null, team.City,
                        team.StateProv, team.Country, team.Website, team.RookieYear, null, null, null);
                    result.Add(transformedTeam);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing team data: {TeamData}",
                        JsonSerializer.Serialize(sortedTeams[i]));
                }

            return Ok(new OffseasonTeamsResponse(result, result.Count, result.Count, 1, 1));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching offseason teams for event {EventCode}", eventCode);
            return NoContent();
        }
    }

    [HttpGet("offseason/events")]
    [RedisCache("tbaapi:offseason:events", RedisCacheTime.OneDay)]
    [OpenApiTag("FRC Offseason")]
    [ProducesResponseType(typeof(OffseasonEventsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetOffseasonEvents(int year)
    {
        Response.Headers.CacheControl = "s-maxage=86400"; // 24 hours

        try
        {
            var tbaResponse = await tbaApiClient.Get<List<TBAEvent>>($"events/{year}");
            if (tbaResponse == null) return NoContent();

            var result = new List<OffseasonEvent>();

            // Transform TBA format to FIRST API format, filter for offseason events (skip index 0 like TypeScript)
            for (var i = 1; i < tbaResponse.Count; i++)
                try
                {
                    var evt = tbaResponse[i];
                    if (evt.EventTypeString != "Offseason") continue;
                    var address = evt.Address ?? "no address, no city, no state, no country";
                    var addressParts = address.Split(", ");

                    var transformedEvent = new OffseasonEvent(evt.Key, evt.EventCode, evt.ShortName,
                        evt.EventTypeString, evt.District?.Abbreviation, evt.LocationName ?? "",
                        addressParts.Length > 0 ? addressParts[0] : "", addressParts.Length > 1 ? addressParts[1] : "",
                        addressParts.Length > 2 ? addressParts[2] : "", addressParts.Length > 3 ? addressParts[3] : "",
                        evt.Website, evt.Timezone ?? "", evt.StartDate, evt.EndDate);
                    result.Add(transformedEvent);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing event data: {EventData}",
                        JsonSerializer.Serialize(tbaResponse[i]));
                }

            return Ok(new OffseasonEventsResponse(result, result.Count));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching offseason events for year {Year}", year);
            return NoContent();
        }
    }

    /// <summary>
    /// Gets team statistics and data for a specific FRC team from Statbotics
    /// </summary>
    /// <param name="year">The competition year/season</param>
    /// <param name="teamNumber">The FRC team number</param>
    /// <returns>Team statistics and data from Statbotics API</returns>
    /// <response code="200">Returns the team's statistics and data</response>
    /// <response code="204">No data found for the specified team and year</response>
    [HttpGet("statbotics/{teamNumber}")]
    [RedisCache("statbotics:team-data", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Team Data")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetStatboticsData(int year, string teamNumber)
    {
        var result = await statboticsApiClient.GetGeneric($"team_year/{teamNumber}/{year}");
        if (result == null) return NoContent();
        return Ok(result);
    }

    #region Private Methods

    private async Task<TeamAwardsResponse?> GetTeamAwards(int season, int team)
    {
        var cacheKey = $"frc:team:{team}:season:{season}:awards";

        var cachedResult = await _redis.StringGetAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedResult)) return JsonSerializer.Deserialize<TeamAwardsResponse?>(cachedResult!);

        try
        {
            var result = await frcApiClient.Get<TeamAwardsResponse>($"{season}/awards/team/{team}");
            var cachePeriod = DateTime.Now.Year == season ? TimeSpan.FromHours(7) : TimeSpan.FromDays(14);
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

    private async Task<List<TeamMedia>?> GetTeamMediaData(int year, int teamNumber)
    {
        var cacheKey = $"tbaapi:team/frc{teamNumber}/media/{year}";

        // Check Redis cache first
        var cachedResult = await _redis.StringGetAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedResult)) return JsonSerializer.Deserialize<List<TeamMedia>?>(cachedResult!);

        try
        {
            var result = await tbaApiClient.Get<List<TeamMedia>>($"team/frc{teamNumber}/media/{year}");

            // Cache for 3 days
            if (result != null)
                await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), TimeSpan.FromDays(3));

            return result;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}