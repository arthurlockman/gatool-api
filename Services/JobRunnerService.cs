using GAToolAPI.Jobs;

namespace GAToolAPI.Services;

public class JobRunnerService(IServiceProvider serviceProvider, ILogger<JobRunnerService> logger)
{
    private readonly Dictionary<string, Type> _availableJobs = new()
    {
        { "UpdateGlobalHighScores", typeof(UpdateGlobalHighScoresJob) },
        { "SyncUsers", typeof(SyncUsersJob) }
    };

    public async Task RunJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        if (!_availableJobs.TryGetValue(jobName, out var jobType))
        {
            logger.LogError("Job '{JobName}' not found. Available jobs: {AvailableJobs}",
                jobName, string.Join(", ", _availableJobs.Keys));
            throw new Exception($"Job '{jobName}' not found.");
        }

        logger.LogInformation("Starting job: {JobName}", jobName);

        try
        {
            var job = (IJob)serviceProvider.GetRequiredService(jobType);
            await job.ExecuteAsync(cancellationToken);

            logger.LogInformation("Job '{JobName}' completed successfully", jobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job '{JobName}' failed with error: {Error}", jobName, ex.Message);
            throw new Exception($"Job '{jobName}' failed with error: {ex.Message}");
        }
    }

    public IEnumerable<string> GetAvailableJobs()
    {
        return _availableJobs.Keys;
    }
}