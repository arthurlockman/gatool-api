using Azure.Security.KeyVault.Secrets;
using GAToolAPI.Extensions;
using GAToolAPI.Models;
using GAToolAPI.Services;
using NewRelic.Api.Agent;

namespace GAToolAPI.Jobs;

public class UpdateGlobalHighScoresJob(
    ILogger<UpdateGlobalHighScoresJob> logger,
    IConfiguration configuration,
    SecretClient secretClient,
    FRCApiService frcApiService,
    ScheduleService scheduleService,
    FTCApiService ftcApiService,
    FTCScheduleService ftcScheduleService,
    UserStorageService userStorageService) : IJob
{
    public string Name => "UpdateGlobalHighScores";

    private static string GetMatchKey(HybridMatch match)
    {
        return $"{match.EventCode}|{match.MatchNumber}|{match.TournamentLevel}|{match.Field ?? ""}";
    }

    [Transaction]
    [Trace]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var isDryRun = configuration.GetValue<bool>("DryRun");
        var lookbackDays = configuration.GetValue("HighScoresLookbackDays", 7);
        var logPrefix = isDryRun ? "[DRY RUN] " : "";

        logger.LogInformation("{Prefix}Starting UpdateGlobalHighScores job (Time window: ±{Days} days)...",
            logPrefix, lookbackDays);

        var errors = new List<Exception>();

        try
        {
            await CalculateFrcHighScores(logPrefix, lookbackDays, isDryRun, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating FRC high scores");
            errors.Add(ex);
        }

        try
        {
            await CalculateFtcHighScores(logPrefix, lookbackDays, isDryRun, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating FTC high scores");
            errors.Add(ex);
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("One or more high score calculations failed", errors);
        }
    }

    private async Task CalculateFrcHighScores(string logPrefix, int lookbackDays, bool isDryRun, CancellationToken cancellationToken)
    {
            var currentYear =
                await secretClient.GetSecretAsync("FRCCurrentSeason", cancellationToken: cancellationToken);
            var year = int.Parse(currentYear.Value.Value);

            // Fetch all events for the season
            var events = await frcApiService.Get<EventListResponse>($"{year}/events");
            logger.LogInformation("{Prefix}Found {EventCount} total events for {Year} season.",
                logPrefix, events?.EventCount, year);

            // Log first few events for debugging
            if (events?.Events is { Count: > 0 })
            {
                foreach (var evt in events.Events.Take(3))
                {
                    logger.LogInformation("{Prefix}Sample event: {Code} - Start: {DateStart}, End: {DateEnd}",
                        logPrefix, evt.Code, evt.DateStart, evt.DateEnd);
                }
            }

            // Filter events to only those within the time window (past or future)
            var now = DateTime.UtcNow;
            var windowStart = now.AddDays(-lookbackDays);
            var windowEnd = now.AddDays(lookbackDays);
            logger.LogInformation("{Prefix}Time window: {WindowStart} to {WindowEnd} (UTC, ±{Days} days)",
                logPrefix, windowStart, windowEnd, lookbackDays);

            var filteredEvents = events?.Events?.Where(e =>
            {
                // Try to parse DateEnd
                if (!DateTimeOffset.TryParse(e.DateEnd, out var dateEnd))
                {
                    logger.LogWarning("{Prefix}Event {EventCode} has unparseable DateEnd '{DateEnd}'. Including event to be safe.",
                        logPrefix, e.Code, e.DateEnd);
                    return true; // Include events with bad dates
                }

                // Try to parse DateStart
                if (!DateTimeOffset.TryParse(e.DateStart, out var dateStart))
                {
                    logger.LogWarning("{Prefix}Event {EventCode} has unparseable DateStart '{DateStart}'. Including event to be safe.",
                        logPrefix, e.Code, e.DateStart);
                    return true; // Include events with bad dates
                }

                // Include event if it overlaps with our time window
                // Event overlaps if: event_start <= window_end AND event_end >= window_start
                return dateStart.UtcDateTime <= windowEnd && dateEnd.UtcDateTime >= windowStart;
            }).ToList() ?? [];

            logger.LogInformation("{Prefix}Filtered to {FilteredCount} events within ±{Days} day window.",
                logPrefix, filteredEvents.Count, lookbackDays);

            if (filteredEvents.Count == 0)
            {
                logger.LogWarning("{Prefix}No events found within time window. " +
                    "This may be expected if the season has ended or hasn't started yet. " +
                    "Time window: {WindowStart} to {WindowEnd} (±{Days} days), Season: {Year}",
                    logPrefix, windowStart, windowEnd, lookbackDays, year);
            }

            // Retrieve existing high scores to extract historical matches
            var existingHighScores = await userStorageService.GetHighScores(year);
            var historicalMatches = existingHighScores
                .Select(hs => hs.MatchData.Match)
                .ToList();

            logger.LogInformation("{Prefix}Retrieved {HistoricalCount} historical matches from existing high scores.",
                logPrefix, historicalMatches.Count);

            // Early exit if no filtered events and no historical matches
            if (filteredEvents.Count == 0 && historicalMatches.Count == 0)
            {
                logger.LogWarning("{Prefix}No events to process and no historical data. Exiting job.",
                    logPrefix);
                return;
            }

            // Fetch matches for filtered events only
            var mutex = new SemaphoreSlim(50);
            var newMatches = (await Task.WhenAll(filteredEvents.Select(async e =>
            {
                try
                {
                    await mutex.WaitAsync(cancellationToken);
                    var qualMatches = await scheduleService.BuildHybridSchedule($"{year}", e.Code, "qual");
                    var playoffMatches = await scheduleService.BuildHybridSchedule($"{year}", e.Code, "playoff");
                    var eventMatches = qualMatches?.Schedule ?? [];
                    eventMatches.AddRange(playoffMatches?.Schedule ?? []);
                    return eventMatches.Select(m =>
                    {
                        m.DistrictCode = e.DistrictCode;
                        m.EventCode = e.Code;
                        return m;
                    }).Where(m =>
                        !string.IsNullOrWhiteSpace(m.PostResultTime) &&
                        !m.Teams.Any(t => t.TeamNumber is >= 9986 and <= 9999));
                }
                finally
                {
                    mutex.Release();
                }
            }))).SelectMany(matches => matches).ToList();

            logger.LogInformation("{Prefix}Fetched {NewMatchCount} new matches from filtered events.",
                logPrefix, newMatches.Count);

            // Merge and deduplicate: prefer newly fetched matches over historical ones
            var matchDictionary = new Dictionary<string, HybridMatch>();

            // Add historical matches first
            foreach (var match in historicalMatches)
            {
                matchDictionary[GetMatchKey(match)] = match;
            }

            // Overwrite with new matches (they take priority)
            foreach (var match in newMatches)
            {
                matchDictionary[GetMatchKey(match)] = match;
            }

            var allMatches = matchDictionary.Values.ToList();
            logger.LogInformation("{Prefix}Merged to {TotalMatches} total unique matches ({NewCount} new, {HistoricalCount} historical).",
                logPrefix, allMatches.Count, newMatches.Count, historicalMatches.Count);

            // Calculate and store global high scores
            var globalHighScores = allMatches.CalculateHighScores(year);
            if (!isDryRun)
            {
                await globalHighScores.StoreHighScores(userStorageService, year);
            }
            logger.LogInformation("{Prefix}Calculated global high scores: {Count} categories.",
                logPrefix, globalHighScores.Count);

            // Retrieve districts and calculate high scores for each
            var districts = await frcApiService.Get<DistrictListResponse>($"{year}/districts");
            await Task.WhenAll(districts?.Districts?.Select(async district =>
            {
                var districtMatches = allMatches.Where(m => m.DistrictCode == district.Code).ToList();
                var districtHighScores = districtMatches.CalculateHighScores(year, $"District{district.Code}");

                logger.LogInformation("{Prefix}District {DistrictCode}: {MatchCount} matches, {ScoreCount} high scores.",
                    logPrefix, district.Code, districtMatches.Count, districtHighScores.Count);

                if (!isDryRun)
                {
                    await districtHighScores.StoreHighScores(userStorageService, year, $"District{district.Code}");
                }
            }) ?? []);

            logger.LogInformation("{Prefix}FRC UpdateGlobalHighScores completed successfully", logPrefix);
    }

    private async Task CalculateFtcHighScores(string logPrefix, int lookbackDays, bool isDryRun, CancellationToken cancellationToken)
    {
            var currentFtcYear =
                await secretClient.GetSecretAsync("FTCCurrentSeason", cancellationToken: cancellationToken);
            var year = int.Parse(currentFtcYear.Value.Value);

            // Fetch all FTC events for the season
            var events = await ftcApiService.Get<FTCEventListResponse>($"{year}/events");
            logger.LogInformation("{Prefix}[FTC] Found {EventCount} total events for {Year} season.",
                logPrefix, events?.EventCount, year);

            // Log first few events for debugging
            if (events?.Events is { Count: > 0 })
            {
                foreach (var evt in events.Events.Take(3))
                {
                    logger.LogInformation("{Prefix}[FTC] Sample event: {Code} - Start: {DateStart}, End: {DateEnd}",
                        logPrefix, evt.Code, evt.DateStart, evt.DateEnd);
                }
            }

            // Filter events to only those within the time window (past or future)
            var now = DateTime.UtcNow;
            var windowStart = now.AddDays(-lookbackDays);
            var windowEnd = now.AddDays(lookbackDays);
            logger.LogInformation("{Prefix}[FTC] Time window: {WindowStart} to {WindowEnd} (UTC, ±{Days} days)",
                logPrefix, windowStart, windowEnd, lookbackDays);

            var filteredEvents = events?.Events?.Where(e =>
            {
                if (!DateTimeOffset.TryParse(e.DateEnd, out var dateEnd))
                {
                    logger.LogWarning("{Prefix}[FTC] Event {EventCode} has unparseable DateEnd '{DateEnd}'. Including event to be safe.",
                        logPrefix, e.Code, e.DateEnd);
                    return true;
                }

                if (!DateTimeOffset.TryParse(e.DateStart, out var dateStart))
                {
                    logger.LogWarning("{Prefix}[FTC] Event {EventCode} has unparseable DateStart '{DateStart}'. Including event to be safe.",
                        logPrefix, e.Code, e.DateStart);
                    return true;
                }

                return dateStart.UtcDateTime <= windowEnd && dateEnd.UtcDateTime >= windowStart;
            }).ToList() ?? [];

            logger.LogInformation("{Prefix}[FTC] Filtered to {FilteredCount} events within ±{Days} day window.",
                logPrefix, filteredEvents.Count, lookbackDays);

            if (filteredEvents.Count == 0)
            {
                logger.LogWarning("{Prefix}[FTC] No events found within time window. " +
                    "This may be expected if the season has ended or hasn't started yet. " +
                    "Time window: {WindowStart} to {WindowEnd} (±{Days} days), Season: {Year}",
                    logPrefix, windowStart, windowEnd, lookbackDays, year);
            }

            // Retrieve existing FTC high scores to extract historical matches
            var existingHighScores = await userStorageService.GetHighScores(year, "FTC-");
            var historicalMatches = existingHighScores
                .Select(hs => hs.MatchData.Match)
                .ToList();

            logger.LogInformation("{Prefix}[FTC] Retrieved {HistoricalCount} historical matches from existing high scores.",
                logPrefix, historicalMatches.Count);

            // Early exit if no filtered events and no historical matches
            if (filteredEvents.Count == 0 && historicalMatches.Count == 0)
            {
                logger.LogWarning("{Prefix}[FTC] No events to process and no historical data. Exiting.",
                    logPrefix);
                return;
            }

            // Fetch matches for filtered events only
            var mutex = new SemaphoreSlim(50);
            var newMatches = (await Task.WhenAll(filteredEvents.Select(async e =>
            {
                try
                {
                    await mutex.WaitAsync(cancellationToken);
                    var qualMatches = await ftcScheduleService.BuildHybridSchedule($"{year}", e.Code, "qual");
                    var playoffMatches = await ftcScheduleService.BuildHybridSchedule($"{year}", e.Code, "playoff");
                    var eventMatches = qualMatches?.Schedule ?? [];
                    eventMatches.AddRange(playoffMatches?.Schedule ?? []);
                    return eventMatches.Select(m =>
                    {
                        m.DistrictCode = e.LeagueCode != null && e.RegionCode != null
                            ? $"{e.RegionCode}-{e.LeagueCode}"
                            : null;
                        m.EventCode = e.Code;
                        return m;
                    }).Where(m => !string.IsNullOrWhiteSpace(m.PostResultTime));
                }
                finally
                {
                    mutex.Release();
                }
            }))).SelectMany(matches => matches).ToList();

            logger.LogInformation("{Prefix}[FTC] Fetched {NewMatchCount} new matches from filtered events.",
                logPrefix, newMatches.Count);

            // Merge and deduplicate: prefer newly fetched matches over historical ones
            var matchDictionary = new Dictionary<string, HybridMatch>();

            // Add historical matches first
            foreach (var match in historicalMatches)
            {
                matchDictionary[GetMatchKey(match)] = match;
            }

            // Overwrite with new matches (they take priority)
            foreach (var match in newMatches)
            {
                matchDictionary[GetMatchKey(match)] = match;
            }

            var allMatches = matchDictionary.Values.ToList();
            logger.LogInformation("{Prefix}[FTC] Merged to {TotalMatches} total unique matches ({NewCount} new, {HistoricalCount} historical).",
                logPrefix, allMatches.Count, newMatches.Count, historicalMatches.Count);

            // Calculate and store global FTC high scores
            var globalHighScores = allMatches.CalculateHighScores(year, "FTC");
            if (!isDryRun)
            {
                await globalHighScores.StoreHighScores(userStorageService, year, "FTC-");
            }
            logger.LogInformation("{Prefix}[FTC] Calculated global high scores: {Count} categories.",
                logPrefix, globalHighScores.Count);

            // Retrieve leagues and calculate high scores for each
            var leagues = await ftcApiService.Get<FTCLeagueListResponse>($"{year}/leagues");
            if (leagues?.Leagues != null)
            {
                await Task.WhenAll(leagues.Leagues.Select(async league =>
                {
                    var leagueKey = $"{league.Region}-{league.Code}";
                    var leagueMatches = allMatches.Where(m => m.DistrictCode == leagueKey).ToList();
                    var leaguePrefix = $"FTCLeague{league.Region}{league.Code}";
                    var leagueHighScores = leagueMatches.CalculateHighScores(year, leaguePrefix);

                    logger.LogInformation("{Prefix}[FTC] League {LeagueKey}: {MatchCount} matches, {ScoreCount} high scores.",
                        logPrefix, leagueKey, leagueMatches.Count, leagueHighScores.Count);

                    if (!isDryRun)
                    {
                        await leagueHighScores.StoreHighScores(userStorageService, year, leaguePrefix);
                    }
                }));
            }

            // Calculate high scores per region (aggregate all matches in a region)
            var regions = allMatches
                .Where(m => m.DistrictCode != null)
                .Select(m => m.DistrictCode!.Split('-')[0])
                .Distinct()
                .ToList();

            foreach (var region in regions)
            {
                var regionMatches = allMatches
                    .Where(m => m.DistrictCode != null && m.DistrictCode.StartsWith($"{region}-"))
                    .ToList();
                var regionPrefix = $"FTCRegion{region}";
                var regionHighScores = regionMatches.CalculateHighScores(year, regionPrefix);

                logger.LogInformation("{Prefix}[FTC] Region {Region}: {MatchCount} matches, {ScoreCount} high scores.",
                    logPrefix, region, regionMatches.Count, regionHighScores.Count);

                if (!isDryRun)
                {
                    await regionHighScores.StoreHighScores(userStorageService, year, regionPrefix);
                }
            }

            logger.LogInformation("{Prefix}[FTC] UpdateGlobalHighScores completed successfully", logPrefix);
    }
}