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

        logger.LogInformation("{Prefix}Starting UpdateGlobalHighScores job (Lookback: {LookbackDays} days)...",
            logPrefix, lookbackDays);

        try
        {
            var currentYear =
                await secretClient.GetSecretAsync("FRCCurrentSeason", cancellationToken: cancellationToken);
            var year = int.Parse(currentYear.Value.Value);

            // Fetch all events for the season
            var events = await frcApiService.Get<EventListResponse>($"{year}/events");
            logger.LogInformation("{Prefix}Found {EventCount} total events for {Year} season.",
                logPrefix, events?.EventCount, year);

            // Log first few events for debugging
            if (events?.Events != null && events.Events.Count > 0)
            {
                foreach (var evt in events.Events.Take(3))
                {
                    logger.LogInformation("{Prefix}Sample event: {Code} - Start: {DateStart}, End: {DateEnd}",
                        logPrefix, evt.Code, evt.DateStart, evt.DateEnd);
                }
            }

            // Filter events to only those within lookback window or in the future
            var now = DateTime.UtcNow;
            var cutoffDate = now.AddDays(-lookbackDays);
            logger.LogInformation("{Prefix}Cutoff date for filtering: {CutoffDate} (UTC)", logPrefix, cutoffDate);

            var filteredEvents = events?.Events?.Where(e =>
            {
                // Try to parse DateEnd
                if (!DateTimeOffset.TryParse(e.DateEnd, out var dateEnd))
                {
                    logger.LogWarning("{Prefix}Event {EventCode} has unparseable DateEnd '{DateEnd}'. Including event to be safe.",
                        logPrefix, e.Code, e.DateEnd);
                    return true; // Include events with bad dates
                }

                // Check if event ended within lookback window
                if (dateEnd.UtcDateTime >= cutoffDate)
                    return true;

                // Try to parse DateStart
                if (!DateTimeOffset.TryParse(e.DateStart, out var dateStart))
                {
                    logger.LogWarning("{Prefix}Event {EventCode} has unparseable DateStart '{DateStart}'. Including event to be safe.",
                        logPrefix, e.Code, e.DateStart);
                    return true; // Include events with bad dates
                }

                // Check if event is in the future
                if (dateStart.UtcDateTime > now)
                    return true;

                return false;
            }).ToList() ?? [];

            logger.LogInformation("{Prefix}Filtered to {FilteredCount} events within lookback window or upcoming.",
                logPrefix, filteredEvents.Count);

            if (filteredEvents.Count == 0)
            {
                logger.LogWarning("{Prefix}No events found within lookback window. " +
                    "This may be expected if the season has ended. " +
                    "Cutoff date: {CutoffDate}, Current date: {Now}, Season: {Year}",
                    logPrefix, cutoffDate, now, year);
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

            logger.LogInformation("{Prefix}UpdateGlobalHighScores job completed successfully", logPrefix);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing UpdateGlobalHighScores job");
            throw;
        }
    }
}