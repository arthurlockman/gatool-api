using System.Text.Json;

namespace GAToolAPI.Services;

public record EcsTaskMetadata
{
    public string? TaskArn { get; init; }
    public string? TaskId { get; init; }
    public string? Cluster { get; init; }
    public string? ClusterName { get; init; }
    public string? Family { get; init; }
    public string? Revision { get; init; }
    public string? AvailabilityZone { get; init; }
    public string? LaunchType { get; init; }

    public bool IsRunningOnEcs => TaskArn != null;
}

public static class EcsMetadataService
{
    public static async Task<EcsTaskMetadata> FetchAsync()
    {
        var metadataUri = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4");
        if (string.IsNullOrEmpty(metadataUri))
            return new EcsTaskMetadata();

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await httpClient.GetStringAsync($"{metadataUri}/task");
            var root = JsonDocument.Parse(response).RootElement;

            var taskArn = root.GetProperty("TaskARN").GetString();
            var cluster = root.GetProperty("Cluster").GetString();

            return new EcsTaskMetadata
            {
                TaskArn = taskArn,
                TaskId = taskArn?.Split('/').LastOrDefault(),
                Cluster = cluster,
                ClusterName = cluster?.Split('/').LastOrDefault(),
                Family = root.GetProperty("Family").GetString(),
                Revision = root.GetProperty("Revision").GetString(),
                AvailabilityZone = root.TryGetProperty("AvailabilityZone", out var az) ? az.GetString() : null,
                LaunchType = root.TryGetProperty("LaunchType", out var lt) ? lt.GetString() : null
            };
        }
        catch (Exception)
        {
            return new EcsTaskMetadata();
        }
    }
}
