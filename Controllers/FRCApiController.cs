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
            var tbaResponse = await tbaApiClient.Get<List<TBATeam>>($"event/{year}{eventCode}/teams");
            if (tbaResponse == null || tbaResponse.Count == 0) return NoContent();

            // Sort teams by team number
            var sortedTeams = tbaResponse.OrderBy(t => t.TeamNumber).ToList();

            var result = new List<OffseasonTeam>();

            // Transform TBA format to FIRST API format
            foreach (var team in sortedTeams)
            {
                try
                {
                    var transformedTeam = new OffseasonTeam(team.TeamNumber, team.Name, team.Nickname, null, team.City,
                        team.StateProv, team.Country, team.Website, team.RookieYear, null, null, null);
                    result.Add(transformedTeam);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing team data: {TeamData}",
                        JsonSerializer.Serialize(team));
                }
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

            // Transform TBA format to FIRST API format, filter for offseason events
            // Include: Offseason (99), Preseason (100), and Unlabeled (-1) events
            foreach (var evt in tbaResponse)
            {
                try
                {
                    // Filter for non-official events: Offseason, Preseason, Unlabeled
                    if (evt.EventTypeString != "Offseason" && 
                        evt.EventTypeString != "Preseason" && 
                        evt.EventTypeString != "Unlabeled") continue;
                    
                    var address = evt.Address ?? "";
                    var addressParts = address.Split(", ");
                    
                    // Parse address components properly
                    // Example: "960 W Hedding St, San Jose, CA 95126, USA"
                    var streetAddress = addressParts.Length > 0 ? addressParts[0] : "";
                    var city = addressParts.Length > 1 ? addressParts[1] : "";
                    var stateAndZip = addressParts.Length > 2 ? addressParts[2] : "";
                    var country = addressParts.Length > 3 ? addressParts[3] : "";

                    var transformedEvent = new OffseasonEvent(
                        evt.Key,
                        evt.EventCode,
                        evt.Name, // Use Name instead of ShortName
                        evt.EventTypeString,
                        evt.District?.Abbreviation,
                        evt.LocationName ?? "",
                        streetAddress,
                        city,
                        stateAndZip, // Keep as-is since it may contain "CA 95126" format
                        country,
                        evt.Website,
                        evt.Timezone ?? "",
                        evt.StartDate,
                        evt.EndDate,
                        evt.FirstEventCode
                    );
                    result.Add(transformedEvent);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing event data: {EventData}",
                        JsonSerializer.Serialize(evt));
                }
            }

            return Ok(new OffseasonEventsResponse(result, result.Count));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching offseason events for year {Year}", year);
            return NoContent();
        }
    }

    [HttpGet("offseason/schedule/hybrid/{eventCode}")]
    [RedisCache("tbaapi:offseason:schedule", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Offseason")]
    [ProducesResponseType(typeof(HybridScheduleResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetOffseasonHybridSchedule(int year, string eventCode)
    {
        Response.Headers.CacheControl = "s-maxage=600"; // 10 minutes

        try
        {
            // Call TBA: /event/{eventCode}/matches
            var tbaResponse = await tbaApiClient.Get<List<TBAMatch>>($"event/{year}{eventCode}/matches");
            if (tbaResponse == null || tbaResponse.Count == 0) return NoContent();

            // Separate qualification and playoff matches
            var qualMatches = tbaResponse.Where(m => m.CompLevel == "qm").ToList();
            var playoffMatches = tbaResponse.Where(m => m.CompLevel != "qm").ToList();

            // Sort playoffs: by comp_level priority (ef, qf, sf, f), then by set_number, then by match_number
            var compLevelOrder = new Dictionary<string, int> { ["ef"] = 0, ["qf"] = 1, ["sf"] = 2, ["f"] = 3 };
            var sortedPlayoffs = playoffMatches
                .OrderBy(m => compLevelOrder.TryGetValue(m.CompLevel ?? "", out var order) ? order : 99)
                .ThenBy(m => m.SetNumber ?? 0)
                .ThenBy(m => m.MatchNumber ?? 0)
                .ToList();

            // Convert TBAMatch -> HybridMatch
            var hybridMatches = new List<HybridMatch>();
            
            // Process qualification matches
            foreach (var m in qualMatches)
            {
                try
                {
                    var hm = CreateHybridMatch(m, m.MatchNumber ?? 0, $"Qualification {m.MatchNumber ?? 0}", "Qual");
                    hybridMatches.Add(hm);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error mapping qual TBAMatch to HybridMatch: {Match}", JsonSerializer.Serialize(m));
                }
            }

            // Determine tournament size by counting unique sets in quarterfinals and semifinals
            var qfSets = sortedPlayoffs.Where(m => m.CompLevel == "qf").Select(m => m.SetNumber).Distinct().Count();
            var sfSets = sortedPlayoffs.Where(m => m.CompLevel == "sf").Select(m => m.SetNumber).Distinct().Count();
            
            // Determine tournament structure
            int tournamentSize;
            if (qfSets >= 4 || sfSets >= 8) tournamentSize = 8;  // 8 alliances (has quarterfinals OR many semifinals)
            else if (sfSets >= 2) tournamentSize = 4;  // 4 alliances (has 2-7 semifinals)
            else tournamentSize = 2;  // 2 alliances (finals only)

            // Process playoff matches with sequential match numbers
            var playoffMatchNumber = 1;
            foreach (var m in sortedPlayoffs)
            {
                try
                {
                    var (description, round) = GetMatchDescription(playoffMatchNumber, tournamentSize);
                    var hm = CreateHybridMatch(m, playoffMatchNumber, description, "Playoff");
                    hybridMatches.Add(hm);
                    playoffMatchNumber++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error mapping playoff TBAMatch to HybridMatch: {Match}", JsonSerializer.Serialize(m));
                }
            }

            var response = new HybridScheduleResponse { Schedule = new HybridSchedule { Schedule = hybridMatches } };
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching offseason hybrid schedule for event {EventCode}", eventCode);
            return NoContent();
        }
    }

    private (string description, int round) GetMatchDescription(int playoffMatchNumber, int tournamentSize)
    {
        // Generate match description based on tournament size and sequential match number
        return tournamentSize switch
        {
            8 => playoffMatchNumber switch
            {
                // 8 alliances
                >= 1 and <= 4 => ($"Match {playoffMatchNumber} (R1)", 1),
                >= 5 and <= 8 => ($"Match {playoffMatchNumber} (R2)", 2),
                >= 9 and <= 10 => ($"Match {playoffMatchNumber} (R3)", 3),
                >= 11 and <= 12 => ($"Match {playoffMatchNumber} (R4)", 4),
                13 => ($"Match {playoffMatchNumber} (R5)", 5),
                14 => ("Final 1", 6),
                15 => ("Final 2", 6),
                _ => ($"Final Tiebreaker {playoffMatchNumber - 15}", 6)  // 16+ => Tiebreaker 1, 2, 3...
            },
            4 => playoffMatchNumber switch
            {
                // 4 alliances
                >= 1 and <= 2 => ($"Match {playoffMatchNumber} (R1)", 1),
                >= 3 and <= 4 => ($"Match {playoffMatchNumber} (R2)", 2),
                5 => ($"Match {playoffMatchNumber} (R3)", 3),
                6 => ("Final 1", 4),
                7 => ("Final 2", 4),
                _ => ($"Final Tiebreaker {playoffMatchNumber - 7}", 4)  // 8+ => Tiebreaker 1, 2, 3...
            },
            2 => playoffMatchNumber switch
            {
                // 2 alliances - all matches are finals
                1 => ("Final 1", 1),
                2 => ("Final 2", 1),
                _ => ($"Final Tiebreaker {playoffMatchNumber - 2}", 1)  // 3+ => Tiebreaker 1, 2, 3...
            },
            _ => ($"Match {playoffMatchNumber}", 1) // Default fallback
        };
    }

    private MatchScore? TransformScoreBreakdown(TBAMatch m, int matchNumber, string tournamentLevel)
    {
        if (m.ScoreBreakdown == null) return null;

        try
        {
            // Parse the score breakdown JSON
            var breakdown = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(m.ScoreBreakdown));
            
            if (breakdown == null) return null;

            var alliances = new List<AllianceScore>();
            
            // Transform Blue alliance
            if (breakdown.TryGetValue("blue", out var blueElement))
            {
                var blueAlliance = TransformAllianceScore(blueElement, "Blue");
                if (blueAlliance != null) alliances.Add(blueAlliance);
            }

            // Transform Red alliance
            if (breakdown.TryGetValue("red", out var redElement))
            {
                var redAlliance = TransformAllianceScore(redElement, "Red");
                if (redAlliance != null) alliances.Add(redAlliance);
            }

            // Create MatchScore record
            var matchScore = new MatchScore(
                MatchLevel: tournamentLevel == "Qual" ? "Qualification" : "Playoff",
                MatchNumber: matchNumber,
                WinningAlliance: DetermineWinningAlliance(m),
                Tiebreaker: new Tiebreaker(-1, ""),
                CoopertitionBonusAchieved: false,
                Alliances: alliances
            )
            {
                AdditionalProperties = new Dictionary<string, object>()
            };

            // Extract and surface bonus properties from alliance details
            ExtractBonusProperties(matchScore);

            return matchScore;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transforming score breakdown for match {MatchNumber}", matchNumber);
            return null;
        }
    }

    private void ExtractBonusProperties(MatchScore matchScore)
    {
        if (matchScore.Alliances == null || matchScore.Alliances.Count == 0) return;
        if (matchScore.AdditionalProperties == null) return;

        // Get all properties from the first alliance that contain "bonus" in their name
        var allianceType = typeof(AllianceScore);
        var bonusProperties = allianceType.GetProperties()
            .Where(p => p.Name.Contains("Bonus", StringComparison.OrdinalIgnoreCase) &&
                       p.Name != "CoopertitionCriteriaMet") // Exclude this, it's not a top-level property
            .ToList();

        // Extract bonus values and add to AdditionalProperties
        foreach (var property in bonusProperties)
        {
            // Convert property name to camelCase for JSON
            var jsonPropertyName = ToCamelCase(property.Name);

            // Check if any alliance has this bonus achieved (OR logic for boolean bonuses)
            if (property.PropertyType == typeof(bool))
            {
                var anyAchieved = matchScore.Alliances
                    .Select(a => (bool?)property.GetValue(a))
                    .Any(v => v == true);
                
                matchScore.AdditionalProperties[jsonPropertyName] = anyAchieved;
            }
            else
            {
                // For non-boolean bonus properties, take the value from the first alliance
                var value = property.GetValue(matchScore.Alliances[0]);
                if (value != null)
                {
                    matchScore.AdditionalProperties[jsonPropertyName] = value;
                }
            }
        }
    }

    private string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;
        return char.ToLower(str[0]) + str.Substring(1);
    }

    private int? DetermineWinningAlliance(TBAMatch m)
    {
        if (m.Alliances == null) return null;
        
        var blueScore = m.Alliances.TryGetValue("blue", out var blue) ? blue.Score : 0;
        var redScore = m.Alliances.TryGetValue("red", out var red) ? red.Score : 0;

        if (blueScore > redScore) return 0; // Blue wins
        if (redScore > blueScore) return 1; // Red wins
        return -1; // Tie
    }

    private AllianceScore? TransformAllianceScore(JsonElement allianceElement, string allianceName)
    {
        try
        {
            // Parse reefs
            Reef? autoReef = null;
            if (allianceElement.TryGetProperty("autoReef", out var autoReefElement))
            {
                autoReef = ParseReef(autoReefElement);
            }

            Reef? teleopReef = null;
            if (allianceElement.TryGetProperty("teleopReef", out var teleopReefElement))
            {
                teleopReef = ParseReef(teleopReefElement);
            }

            // Create AllianceScore record
            return new AllianceScore(
                Alliance: allianceName,
                AutoLineRobot1: GetStringValue(allianceElement, "autoLineRobot1"),
                EndGameRobot1: GetStringValue(allianceElement, "endGameRobot1"),
                AutoLineRobot2: GetStringValue(allianceElement, "autoLineRobot2"),
                EndGameRobot2: GetStringValue(allianceElement, "endGameRobot2"),
                AutoLineRobot3: GetStringValue(allianceElement, "autoLineRobot3"),
                EndGameRobot3: GetStringValue(allianceElement, "endGameRobot3"),
                AutoReef: autoReef,
                AutoCoralCount: GetIntValue(allianceElement, "autoCoralCount"),
                AutoMobilityPoints: GetIntValue(allianceElement, "autoMobilityPoints"),
                AutoPoints: GetIntValue(allianceElement, "autoPoints"),
                AutoCoralPoints: GetIntValue(allianceElement, "autoCoralPoints"),
                TeleopReef: teleopReef,
                TeleopCoralCount: GetIntValue(allianceElement, "teleopCoralCount"),
                TeleopPoints: GetIntValue(allianceElement, "teleopPoints"),
                TeleopCoralPoints: GetIntValue(allianceElement, "teleopCoralPoints"),
                AlgaePoints: GetIntValue(allianceElement, "algaePoints"),
                NetAlgaeCount: GetIntValue(allianceElement, "netAlgaeCount"),
                WallAlgaeCount: GetIntValue(allianceElement, "wallAlgaeCount"),
                EndGameBargePoints: GetIntValue(allianceElement, "endGameBargePoints"),
                AutoBonusAchieved: GetBoolValue(allianceElement, "autoBonusAchieved"),
                CoralBonusAchieved: GetBoolValue(allianceElement, "coralBonusAchieved"),
                BargeBonusAchieved: GetBoolValue(allianceElement, "bargeBonusAchieved"),
                CoopertitionCriteriaMet: false, // TBA doesn't provide this
                FoulCount: GetIntValue(allianceElement, "foulCount"),
                TechFoulCount: GetIntValue(allianceElement, "techFoulCount"),
                G206Penalty: GetBoolValue(allianceElement, "g206Penalty"),
                G410Penalty: GetBoolValue(allianceElement, "g410Penalty"),
                G418Penalty: GetBoolValue(allianceElement, "g418Penalty"),
                G428Penalty: GetBoolValue(allianceElement, "g428Penalty"),
                AdjustPoints: 0, // TBA doesn't provide this
                FoulPoints: GetIntValue(allianceElement, "foulPoints"),
                Rp: GetIntValue(allianceElement, "rp"),
                TotalPoints: GetIntValue(allianceElement, "totalPoints")
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transforming alliance score for {AllianceName}", allianceName);
            return null;
        }
    }

    private Reef ParseReef(JsonElement reefElement)
    {
        ReefRow? topRow = null;
        if (reefElement.TryGetProperty("topRow", out var topRowElement))
        {
            topRow = ParseReefRow(topRowElement);
        }
        
        ReefRow? midRow = null;
        if (reefElement.TryGetProperty("midRow", out var midRowElement))
        {
            midRow = ParseReefRow(midRowElement);
        }
        
        ReefRow? botRow = null;
        if (reefElement.TryGetProperty("botRow", out var botRowElement))
        {
            botRow = ParseReefRow(botRowElement);
        }
        
        var trough = GetIntValue(reefElement, "trough");
        
        return new Reef(topRow, midRow, botRow, trough);
    }

    private ReefRow ParseReefRow(JsonElement rowElement)
    {
        return new ReefRow(
            NodeA: GetBoolValue(rowElement, "nodeA"),
            NodeB: GetBoolValue(rowElement, "nodeB"),
            NodeC: GetBoolValue(rowElement, "nodeC"),
            NodeD: GetBoolValue(rowElement, "nodeD"),
            NodeE: GetBoolValue(rowElement, "nodeE"),
            NodeF: GetBoolValue(rowElement, "nodeF"),
            NodeG: GetBoolValue(rowElement, "nodeG"),
            NodeH: GetBoolValue(rowElement, "nodeH"),
            NodeI: GetBoolValue(rowElement, "nodeI"),
            NodeJ: GetBoolValue(rowElement, "nodeJ"),
            NodeK: GetBoolValue(rowElement, "nodeK"),
            NodeL: GetBoolValue(rowElement, "nodeL")
        );
    }

    private string? GetStringValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String 
            ? prop.GetString() 
            : null;
    }

    private int GetIntValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number 
            ? prop.GetInt32() 
            : 0;
    }

    private bool GetBoolValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && 
               (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False) 
            ? prop.GetBoolean() 
            : false;
    }

    private HybridMatch CreateHybridMatch(TBAMatch m, int matchNumber, string description, string tournamentLevel)
    {
        // Detect if match has been played by checking if score breakdown exists
        var matchHasBeenPlayed = m.ScoreBreakdown != null;
        
        var hm = new HybridMatch
        {
            Field = null,
            StartTime = m.Time.HasValue ? DateTimeOffset.FromUnixTimeSeconds(m.Time.Value).ToString("o") : null,
            AutoStartTime = m.PostResultTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(m.PostResultTime.Value).ToString("o") : null,
            MatchVideoLink = m.Videos != null && m.Videos.Count > 0 ? m.Videos[0].Key : null,
            MatchNumber = matchNumber,
            IsReplay = false,
            ActualStartTime = m.ActualTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(m.ActualTime.Value).ToString("o") : null,
            TournamentLevel = tournamentLevel,
            PostResultTime = m.PostResultTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(m.PostResultTime.Value).ToString("o") : null,
            Description = description,
            ScoreRedFinal = m.Alliances != null && m.Alliances.TryGetValue("red", out var v1) ? v1.Score : null,
            ScoreBlueFinal = m.Alliances != null && m.Alliances.TryGetValue("blue", out var v2) ? v2.Score : null,
            Teams = [],
            EventCode = m.EventKey,
            MatchScores = matchHasBeenPlayed ? TransformScoreBreakdown(m, matchNumber, tournamentLevel) : null
        };

        // If match has been played but time fields are missing, populate them
        if (matchHasBeenPlayed && m.Time.HasValue)
        {
            var startTime = DateTimeOffset.FromUnixTimeSeconds(m.Time.Value);
            
            // Use startTime for actualStartTime if not provided
            if (hm.ActualStartTime == null)
            {
                hm.ActualStartTime = startTime.ToString("o");
            }
            
            // Use startTime for autoStartTime if not provided
            if (hm.AutoStartTime == null)
            {
                hm.AutoStartTime = startTime.ToString("o");
            }
            
            // Use startTime + 3 minutes for postResultTime if not provided
            if (hm.PostResultTime == null)
            {
                hm.PostResultTime = startTime.AddMinutes(3).ToString("o");
            }
        }

        // Map teams: red then blue, assign station names Red 1..3 and Blue 1..3
        if (m.Alliances != null)
        {
            if (m.Alliances.TryGetValue("red", out var red))
            {
                for (var idx = 0; idx < red.TeamKeys.Count; idx++)
                {
                    var teamKey = red.TeamKeys[idx];
                    var teamNumber = int.TryParse(teamKey.Replace("frc", ""), out var tn) ? tn : 0;
                    var surrogate = red.SurrogateTeamKeys?.Contains(teamKey) ?? false;
                    hm.Teams.Add(new HybridTeam { TeamNumber = teamNumber, Station = $"Red {idx + 1}", Surrogate = surrogate });
                }
            }

            if (m.Alliances.TryGetValue("blue", out var blue))
            {
                for (var idx = 0; idx < blue.TeamKeys.Count; idx++)
                {
                    var teamKey = blue.TeamKeys[idx];
                    var teamNumber = int.TryParse(teamKey.Replace("frc", ""), out var tn) ? tn : 0;
                    var surrogate = blue.SurrogateTeamKeys != null && blue.SurrogateTeamKeys.Contains(teamKey);
                    hm.Teams.Add(new HybridTeam { TeamNumber = teamNumber, Station = $"Blue {idx + 1}", Surrogate = surrogate });
                }
            }
        }

        return hm;
    }

    /// <summary>
    /// Gets playoff alliance selections for an offseason FRC event from The Blue Alliance
    /// </summary>
    /// <param name="year">The competition year/season</param>
    /// <param name="eventCode">The event code (e.g., "cc" for Chezy Champs)</param>
    /// <returns>Alliance selections with team picks for each alliance</returns>
    /// <response code="200">Returns the alliance selections with captain and team picks</response>
    /// <response code="204">No alliances found for the specified event</response>
    [HttpGet("offseason/alliances/{eventCode}")]
    [RedisCache("tbaapi:offseason:alliances", RedisCacheTime.OneDay)]
    [OpenApiTag("FRC Offseason")]
    [ProducesResponseType(typeof(AlliancesResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetOffseasonAlliances(int year, string eventCode)
    {
        Response.Headers.CacheControl = "s-maxage=86400"; // 24 hours

        try
        {
            // Call TBA: /event/{eventCode}/alliances
            var tbaResponse = await tbaApiClient.Get<List<TBAAlliance>>($"event/{year}{eventCode}/alliances");
            if (tbaResponse == null || tbaResponse.Count == 0) return NoContent();

            // Transform TBA format to FIRST API format
            var alliances = new List<Alliance>();
            for (var i = 0; i < tbaResponse.Count; i++)
            {
                var tbaAlliance = tbaResponse[i];
                if (tbaAlliance.Picks == null || tbaAlliance.Picks.Count == 0) continue;

                // Parse team numbers from "frcXXXX" format
                var teamNumbers = tbaAlliance.Picks
                    .Select(pick => int.TryParse(pick.Replace("frc", ""), out var num) ? num : (int?)null)
                    .Where(num => num.HasValue)
                    .Select(num => num!.Value)
                    .ToList();

                if (teamNumbers.Count < 2) continue; // Need at least captain and round1

                var alliance = new Alliance(
                    Number: i + 1,
                    Captain: teamNumbers[0],
                    Round1: teamNumbers[1],
                    Round2: teamNumbers.Count > 2 ? teamNumbers[2] : null,
                    Round3: teamNumbers.Count > 3 ? teamNumbers[3] : null,
                    Backup: null, // TBA doesn't provide backup in this format
                    BackupReplaced: null, // TBA doesn't provide backup replaced in this format
                    Name: $"Alliance {i + 1}"
                );

                alliances.Add(alliance);
            }

            var response = new AlliancesResponse(alliances, alliances.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching offseason alliances for event {EventCode}", eventCode);
            return NoContent();
        }
    }

    /// <summary>
    /// Gets qualification rankings for an offseason FRC event from The Blue Alliance
    /// </summary>
    /// <param name="year">The competition year/season</param>
    /// <param name="eventCode">The event code (e.g., "iri" for Indiana Robotics Invitational)</param>
    /// <returns>Event rankings data including team rankings and sort order information</returns>
    /// <response code="200">Returns the event rankings with team standings and sort order details</response>
    /// <response code="204">No rankings data found for the specified event</response>
    [HttpGet("offseason/rankings/{eventCode}")]
    [RedisCache("tbaapi:offseason:rankings", RedisCacheTime.FiveMinutes)]
    [OpenApiTag("FRC Offseason")]
    [ProducesResponseType(typeof(RankingsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetOffseasonRankings(int year, string eventCode)
    {
        Response.Headers.CacheControl = "s-maxage=600"; // 10 minutes

        try
        {
            // Call TBA: /event/{eventCode}/rankings
            var tbaResponse = await tbaApiClient.Get<TBAEventRankings>($"event/{year}{eventCode}/rankings");
            if (tbaResponse == null || tbaResponse.Rankings == null || tbaResponse.Rankings.Count == 0) return NoContent();
            
            // Transform TBA format to FIRST API format
            var rankings = new List<TeamRanking>();
            foreach (var tbaRanking in tbaResponse.Rankings)
            {
                // Extract team number from team_key (e.g., "frc254" -> 254)
                var teamNumberStr = tbaRanking.TeamKey.Replace("frc", "");
                if (!int.TryParse(teamNumberStr, out var teamNumber))
                {
                    logger.LogWarning("Could not parse team number from team_key: {TeamKey}", tbaRanking.TeamKey);
                    continue;
                }

                // Map sort_orders array to individual sortOrder properties (pad with 0 if not enough values)
                var sortOrders = tbaRanking.SortOrders ?? new List<double>();
                var ranking = new TeamRanking(
                    tbaRanking.Rank,
                    teamNumber,
                    sortOrders.Count > 0 ? sortOrders[0] : 0,
                    sortOrders.Count > 1 ? sortOrders[1] : 0,
                    sortOrders.Count > 2 ? sortOrders[2] : 0,
                    sortOrders.Count > 3 ? sortOrders[3] : 0,
                    sortOrders.Count > 4 ? sortOrders[4] : 0,
                    sortOrders.Count > 5 ? sortOrders[5] : 0,
                    tbaRanking.Record?.Wins ?? 0,
                    tbaRanking.Record?.Losses ?? 0,
                    tbaRanking.Record?.Ties ?? 0,
                    tbaRanking.QualAverage ?? 0,
                    tbaRanking.Dq ?? 0,
                    tbaRanking.MatchesPlayed ?? 0
                );
                rankings.Add(ranking);
            }

            // Return in FIRST API format
            return Ok(new RankingsResponse(new RankingsData(rankings), null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching offseason rankings for event {EventCode}", eventCode);
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
    [ProducesResponseType(typeof(StatboticsTeamData), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetStatboticsData(int year, string teamNumber)
    {
        var result = await statboticsApiClient.Get<StatboticsTeamData>($"team_year/{teamNumber}/{year}");
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