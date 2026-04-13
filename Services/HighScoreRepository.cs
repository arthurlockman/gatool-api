using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GAToolAPI.Models;

namespace GAToolAPI.Services;

public class HighScoreRepository(
    IAmazonDynamoDB dynamoDbClient,
    IConfiguration configuration,
    ILogger<HighScoreRepository> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _tableName =
        configuration["DynamoDB:HighScoresTable"] ?? "gatool-high-scores";

    public static string BuildKeyPrefix(ScoreProgram program, ScoreScope scope, params string[] segments)
    {
        var parts = new List<string> { program.ToString(), scope.ToString().ToLowerInvariant() };
        parts.AddRange(segments);
        return string.Join(":", parts);
    }

    public async Task<List<HighScore>> GetHighScores(int year, ScoreProgram program, ScoreScope scope,
        params string[] segments)
    {
        var prefix = BuildKeyPrefix(program, scope, segments);

        // "Year" is a DynamoDB reserved word, so use an expression attribute name
        var keyCondition = "#yr = :year AND begins_with(ScoreKey, :prefix)";
        var expressionValues = new Dictionary<string, AttributeValue>
        {
            [":year"] = new() { N = year.ToString() },
            [":prefix"] = new() { S = $"{prefix}:" }
        };
        var expressionNames = new Dictionary<string, string>
        {
            ["#yr"] = "Year"
        };

        var highScores = new List<HighScore>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = keyCondition,
                ExpressionAttributeValues = expressionValues,
                ExpressionAttributeNames = expressionNames
            };

            if (exclusiveStartKey is { Count: > 0 })
                request.ExclusiveStartKey = exclusiveStartKey;

            var response = await dynamoDbClient.QueryAsync(request);

            foreach (var item in response.Items)
            {
                try
                {
                    if (item.TryGetValue("Data", out var dataAttr))
                    {
                        var highScore = JsonSerializer.Deserialize<HighScore>(dataAttr.S, _jsonOptions);
                        if (highScore != null)
                            highScores.Add(highScore);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to deserialize high score item");
                }
            }

            exclusiveStartKey = response.LastEvaluatedKey;
        } while (exclusiveStartKey is { Count: > 0 });

        return highScores;
    }

    public async Task StoreHighScore(int year, HighScore highScore, ScoreProgram program, ScoreScope scope,
        params string[] segments)
    {
        var prefix = BuildKeyPrefix(program, scope, segments);
        var scoreKey = $"{prefix}:{highScore.Type}:{highScore.Level}";
        var data = JsonSerializer.Serialize(highScore, _jsonOptions);

        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Year"] = new() { N = year.ToString() },
                ["ScoreKey"] = new() { S = scoreKey },
                ["Data"] = new() { S = data }
            }
        };

        // Retry with exponential backoff for throttling during bulk writes
        const int maxRetries = 5;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await dynamoDbClient.PutItemAsync(request);
                return;
            }
            catch (ProvisionedThroughputExceededException) when (attempt < maxRetries)
            {
                var delay = (int)Math.Pow(2, attempt) * 100;
                logger.LogWarning("DynamoDB throttled on {ScoreKey}, retrying in {Delay}ms (attempt {Attempt}/{Max})",
                    scoreKey, delay, attempt + 1, maxRetries);
                await Task.Delay(delay);
            }
            catch (Amazon.DynamoDBv2.Model.ThrottlingException) when (attempt < maxRetries)
            {
                var delay = (int)Math.Pow(2, attempt) * 100;
                logger.LogWarning("DynamoDB throttled on {ScoreKey}, retrying in {Delay}ms (attempt {Attempt}/{Max})",
                    scoreKey, delay, attempt + 1, maxRetries);
                await Task.Delay(delay);
            }
        }
    }
}
