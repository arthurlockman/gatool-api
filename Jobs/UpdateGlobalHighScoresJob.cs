using Azure.Security.KeyVault.Secrets;
using GAToolAPI.Extensions;
using GAToolAPI.Models;
using GAToolAPI.Services;
using NewRelic.Api.Agent;

namespace GAToolAPI.Jobs;

public class UpdateGlobalHighScoresJob(
    ILogger<UpdateGlobalHighScoresJob> logger,
    SecretClient secretClient,
    FRCApiService frcApiService,
    ScheduleService scheduleService,
    UserStorageService userStorageService) : IJob
{
    public string Name => "UpdateGlobalHighScores";

    [Transaction]
    [Trace]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting UpdateGlobalHighScores job...");

        try
        {
            var currentYear =
                await secretClient.GetSecretAsync("FRCCurrentSeason", cancellationToken: cancellationToken);
            var year = int.Parse(currentYear.Value.Value);

            var events = await frcApiService.Get<EventListResponse>($"{year}/events");
            logger.LogInformation("Found {EventsEventCount} events for {ValueValue} season.", events?.EventCount, year);

            var mutex = new SemaphoreSlim(50);
            var allMatches = (await Task.WhenAll(events?.Events?.Select(async e =>
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
                    ;
                }
                finally
                {
                    mutex.Release();
                }
            }) ?? [])).SelectMany(matches => matches).ToList();
            logger.LogInformation("Found {AllMatchesCount} matches for {Year} season.", allMatches.Count, year);
            await allMatches.CalculateHighScores(year).StoreHighScores(userStorageService, year);

            // Retrieve districts and calculate high scores for each
            var districts = await frcApiService.Get<DistrictListResponse>($"{year}/districts");
            await Task.WhenAll(districts?.Districts?.Select(district => allMatches
                .Where(m => m.DistrictCode == district.Code)
                .CalculateHighScores(year)
                .StoreHighScores(userStorageService, year, $"District{district.Code}")) ?? []);

            logger.LogInformation("UpdateGlobalHighScores job completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing UpdateGlobalHighScores job");
            throw;
        }
    }
}