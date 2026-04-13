using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.S3;
using Amazon.S3.Model;

namespace GAToolAPI.Services;

public class UserStorageService(
    IAmazonS3 s3Client,
    IConfiguration configuration)
{
    private readonly JsonSerializerOptions _camelCaseIgnoreCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _teamUpdatesBucket =
        configuration["S3:TeamUpdatesBucket"] ?? "gatool-team-updates";

    private readonly string _teamUpdatesHistoryBucket =
        configuration["S3:TeamUpdatesHistoryBucket"] ?? "gatool-team-updates-history";

    private readonly string _userPrefsBucket =
        configuration["S3:UserPreferencesBucket"] ?? "gatool-user-preferences";

    public async Task<string?> GetUserPreferences(string username)
    {
        return await GetObjectStringOrNull(_userPrefsBucket, $"{username}.prefs.json");
    }

    public async Task StoreUserPreferences(string username, JsonObject preferences)
    {
        await PutObjectString(_userPrefsBucket, $"{username}.prefs.json", preferences.ToJsonString());
    }

    public async Task<string?> GetGlobalAnnouncements()
    {
        return await GetObjectStringOrNull(_userPrefsBucket, "system.announce.json");
    }

    public async Task StoreGlobalAnnouncements(JsonObject announcements)
    {
        await PutObjectString(_userPrefsBucket, "system.announce.json", announcements.ToJsonString());
    }

    public async Task<string?> GetEventAnnouncements(string eventCode)
    {
        return await GetObjectStringOrNull(_userPrefsBucket, $"{eventCode}.announce.json");
    }

    public async Task StoreEventAnnouncements(string eventCode, string announcements)
    {
        await PutObjectString(_userPrefsBucket, $"{eventCode}.announce.json", announcements);
    }

    public async Task<string?> GetTeamUpdates(string teamNumber, bool ftc = false)
    {
        var prefix = ftc ? "ftc/" : "";
        return await GetObjectStringOrNull(_teamUpdatesBucket, $"{prefix}{teamNumber}.json");
    }

    public async Task StoreTeamUpdates(string teamNumber, JsonObject data, string email, bool ftc = false)
    {
        var blobPrefix = ftc ? "ftc/" : "";
        var updateKey = $"{blobPrefix}{teamNumber}.json";
        try
        {
            var currentContent = await GetObjectStringOrNull(_teamUpdatesBucket, updateKey);
            if (currentContent != null)
            {
                // Archive current version to history with current UTC timestamp
                var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var historyKey = $"{blobPrefix}{teamNumber}/{timestamp}.json";
                await PutObjectString(_teamUpdatesHistoryBucket, historyKey, currentContent);
            }
        }
        catch
        {
            // No stored updates, continue without saving history
        }

        data["source"] = JsonValue.Create(email);
        await PutObjectString(_teamUpdatesBucket, updateKey, data.ToJsonString());
    }

    public async Task<IEnumerable<JsonObject>> GetTeamUpdateHistory(string teamNumber, bool ftc = false)
    {
        var blobPrefix = ftc ? "ftc/" : "";
        var prefix = $"{blobPrefix}{teamNumber}/";

        var keys = await ListAllKeys(_teamUpdatesHistoryBucket, prefix);

        var tasks = keys.Select(async key =>
        {
            try
            {
                var content = await GetObjectStringOrNull(_teamUpdatesHistoryBucket, key);
                if (content == null) return null;

                if (JsonNode.Parse(content) is not JsonObject updateData) return null;
                var modifiedDate = key
                    .Replace(prefix, "")
                    .Replace(".json", "");

                updateData["modifiedDate"] = JsonValue.Create(modifiedDate);
                return updateData;
            }
            catch
            {
                return null;
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.OfType<JsonObject>().ToList();
    }

    public async Task RecordWebhookEvent(string eventType, string email)
    {
        const int maxRecentEvents = 50;
        WebhookActivityLog activity;

        try
        {
            var existing = await GetObjectStringOrNull(_userPrefsBucket, "system.userSync.json");
            activity = existing != null
                ? JsonSerializer.Deserialize<WebhookActivityLog>(existing, _camelCaseIgnoreCaseOptions) ??
                  new WebhookActivityLog()
                : new WebhookActivityLog();
        }
        catch
        {
            activity = new WebhookActivityLog();
        }

        activity.LastUpdated = DateTimeOffset.UtcNow;
        activity.TotalEvents++;

        switch (eventType)
        {
            case "subscribe":
                activity.Subscribes++;
                break;
            case "unsubscribe":
                activity.Unsubscribes++;
                break;
            case "profile":
                activity.ProfileUpdates++;
                break;
            case "cleaned":
                activity.Cleaned++;
                break;
        }

        activity.RecentEvents.Insert(0, new WebhookEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = eventType,
            Email = email
        });

        if (activity.RecentEvents.Count > maxRecentEvents)
            activity.RecentEvents.RemoveRange(maxRecentEvents, activity.RecentEvents.Count - maxRecentEvents);

        await PutObjectString(_userPrefsBucket, "system.userSync.json",
            JsonSerializer.Serialize(activity, _camelCaseIgnoreCaseOptions));
    }

    public async Task<string?> GetUserSyncResults()
    {
        return await GetObjectStringOrNull(_userPrefsBucket, "system.userSync.json");
    }

    public async Task SaveUserSyncResults(int fullUsers, int readOnlyUsers, int deletedUsers)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var data = new { timestamp, fullUsers, readOnlyUsers, deletedUsers };
        await PutObjectString(_userPrefsBucket, "system.userSync.json",
            JsonSerializer.Serialize(data, _camelCaseIgnoreCaseOptions));
    }

    public class WebhookActivityLog
    {
        public DateTimeOffset LastUpdated { get; set; }
        public int TotalEvents { get; set; }
        public int Subscribes { get; set; }
        public int Unsubscribes { get; set; }
        public int ProfileUpdates { get; set; }
        public int Cleaned { get; set; }
        public List<WebhookEvent> RecentEvents { get; set; } = [];
    }

    public class WebhookEvent
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Type { get; set; } = "";
        public string Email { get; set; } = "";
    }

    private async Task<string?> GetObjectStringOrNull(string bucket, string key)
    {
        try
        {
            var response = await s3Client.GetObjectAsync(bucket, key);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task PutObjectString(string bucket, string key, string content)
    {
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            ContentBody = content,
            ContentType = "application/json"
        });
    }

    /// <summary>
    ///     Lists all object keys with a given prefix, handling S3 pagination.
    /// </summary>
    private async Task<List<string>> ListAllKeys(string bucket, string prefix)
    {
        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken
            });

            keys.AddRange(response.S3Objects.Select(o => o.Key));
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken != null);

        return keys;
    }
}