using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.Storage.Blobs;
using GAToolAPI.Models;

namespace GAToolAPI.Services;

public class UserStorageService(BlobServiceClient blobServiceClient, ILogger<UserStorageService> logger)
{
    private readonly JsonSerializerOptions _camelCaseIgnoreCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly BlobContainerClient _highScoresClient =
        blobServiceClient.GetBlobContainerClient("gatool-high-scores");

    private readonly BlobContainerClient _teamUpdatesClient =
        blobServiceClient.GetBlobContainerClient("gatool-team-updates");

    private readonly BlobContainerClient _teamUpdatesHistoryClient =
        blobServiceClient.GetBlobContainerClient("gatool-team-updates-history");

    private readonly BlobContainerClient _userPrefsClient =
        blobServiceClient.GetBlobContainerClient("gatool-user-preferences");

    public async Task<string?> GetUserPreferences(string username)
    {
        var blob = _userPrefsClient.GetBlobClient($"{username}.prefs.json");
        var content = await blob.DownloadAsync();
        if (!content.HasValue) return null;
        var reader = new StreamReader(content.Value.Content);
        return await reader.ReadToEndAsync();
    }

    public async Task StoreUserPreferences(string username, JsonObject preferences)
    {
        var blob = _userPrefsClient.GetBlobClient($"{username}.prefs.json");
        var prefString = preferences.ToJsonString();
        await using var stream = await blob.OpenWriteAsync(true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(prefString);
    }

    public async Task<string?> GetGlobalAnnouncements()
    {
        try
        {
            var blob = _userPrefsClient.GetBlobClient("system.announce.json");
            var content = await blob.DownloadAsync();
            if (!content.HasValue) return null;
            var reader = new StreamReader(content.Value.Content);
            return await reader.ReadToEndAsync();
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task StoreGlobalAnnouncements(JsonObject announcements)
    {
        var blob = _userPrefsClient.GetBlobClient("system.announce.json");
        var prefString = announcements.ToJsonString();
        await using var stream = await blob.OpenWriteAsync(true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(prefString);
    }

    public async Task<string?> GetEventAnnouncements(string eventCode)
    {
        try
        {
            var blob = _userPrefsClient.GetBlobClient($"{eventCode}.announce.json");
            var content = await blob.DownloadAsync();
            if (!content.HasValue) return null;
            var reader = new StreamReader(content.Value.Content);
            return await reader.ReadToEndAsync();
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task StoreEventAnnouncements(string eventCode, string announcements)
    {
        var blob = _userPrefsClient.GetBlobClient($"{eventCode}.announce.json");
        await using var stream = await blob.OpenWriteAsync(true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(announcements);
    }

    public async Task<string?> GetTeamUpdates(string teamNumber, bool ftc = false)
    {
        try
        {
            var prefix = ftc ? "ftc/" : "";
            var blob = _teamUpdatesClient.GetBlobClient($"{prefix}{teamNumber}.json");
            var content = await blob.DownloadAsync();
            if (!content.HasValue) return null;
            var reader = new StreamReader(content.Value.Content);
            return await reader.ReadToEndAsync();
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task StoreTeamUpdates(string teamNumber, JsonObject data, string email, bool ftc = false)
    {
        var blobPrefix = ftc ? "ftc/" : "";
        var updateBlobName = $"{blobPrefix}{teamNumber}.json";
        try
        {
            var blob = _teamUpdatesClient.GetBlobClient(updateBlobName);
            var properties = await blob.GetPropertiesAsync();
            var lastModifiedDate = properties.Value.LastModified;

            var currentContent = await blob.DownloadAsync();
            using var reader = new StreamReader(currentContent.Value.Content);
            var content = await reader.ReadToEndAsync();

            var historyBlob =
                _teamUpdatesHistoryClient.GetBlobClient(
                    $"{blobPrefix}{teamNumber}/{lastModifiedDate:yyyy-MM-ddTHH:mm:ss.fffZ}.json");
            await using var historyStream = await historyBlob.OpenWriteAsync(true);
            await using var historyWriter = new StreamWriter(historyStream);
            await historyWriter.WriteAsync(content);
        }
        catch
        {
            // No stored updates, continue without saving history
        }

        data["source"] = JsonValue.Create(email);
        var dataString = data.ToJsonString();

        var userBlob = _teamUpdatesClient.GetBlobClient(updateBlobName);
        await using var stream = await userBlob.OpenWriteAsync(true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(dataString);
    }

    public async Task<IEnumerable<JsonObject>> GetTeamUpdateHistory(string teamNumber, bool ftc = false)
    {
        var results = new List<JsonObject>();

        var blobPrefix = ftc ? "ftc/" : "";
        var blobs = _teamUpdatesHistoryClient.GetBlobsAsync(prefix: $"{blobPrefix}{teamNumber}/");

        await foreach (var blob in blobs)
            try
            {
                var blobClient = _teamUpdatesHistoryClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync();
                var jsonString = content.Value.Content.ToString();

                if (JsonNode.Parse(jsonString) is not JsonObject updateData) continue;
                // Extract the modification date from the blob name
                // Format: {teamNumber}/{timestamp}.json -> timestamp
                var modifiedDate = blob.Name
                    .Replace($"{blobPrefix}{teamNumber}/", "")
                    .Replace(".json", "");

                updateData["modifiedDate"] = JsonValue.Create(modifiedDate);
                results.Add(updateData);
            }
            catch
            {
                // Skip corrupted or invalid blobs
            }

        return results;
    }

    public async Task<List<HighScore>> GetHighScores(int year, string? typePrefix = null)
    {
        var results = new List<HighScore>();

        // List all blobs with the year prefix
        var blobPrefix = typePrefix != null ? $"{year}-{typePrefix}" : year.ToString();
        var blobs = _highScoresClient.GetBlobsAsync(prefix: blobPrefix);

        await foreach (var blob in blobs)
            try
            {
                // Download and parse each high score blob
                var blobClient = _highScoresClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync();
                var jsonString = content.Value.Content.ToString();

                // Parse the JSON - could be a single HighScore or an array
                var jsonNode = JsonNode.Parse(jsonString);
                if (jsonNode is JsonArray jsonArray)
                {
                    // Handle array of high scores
                    results.AddRange(jsonArray.Select(item => item?.Deserialize<HighScore>(_camelCaseIgnoreCaseOptions))
                        .OfType<HighScore>());
                }
                else
                {
                    // Handle single high score
                    var highScore = jsonNode?.Deserialize<HighScore>(_camelCaseIgnoreCaseOptions);
                    if (highScore != null)
                        results.Add(highScore);
                }
            }
            catch (Exception ex)
            {
                // Skip corrupted or invalid blobs
                logger.LogError(ex, ex.Message);
            }

        return results;
    }

    public async Task StoreHighScore(int year, HighScore highScore, string? typePrefix = null)
    {
        var dataString = JsonSerializer.Serialize(highScore, _camelCaseIgnoreCaseOptions);
        var blob = _highScoresClient.GetBlobClient($"{year}-{typePrefix}{highScore.Type}-{highScore.Level}.json");
        await using var stream = await blob.OpenWriteAsync(true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(dataString);
    }

    public async Task SaveUserSyncResults(int fullUsers, int readOnlyUsers, int deletedUsers)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var blob = _userPrefsClient.GetBlobClient("system.userSync.json");
        var data = new
        {
            timestamp,
            fullUsers,
            readOnlyUsers,
            deletedUsers
        };
        await using var stream = await blob.OpenWriteAsync(true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(JsonSerializer.Serialize(data, _camelCaseIgnoreCaseOptions));
    }

    public async Task<string?> GetUserSyncResults()
    {
        var blob = _userPrefsClient.GetBlobClient("system.userSync.json");
        var content = await blob.DownloadAsync();
        if (!content.HasValue) return null;
        var reader = new StreamReader(content.Value.Content);
        return await reader.ReadToEndAsync();
    }
}