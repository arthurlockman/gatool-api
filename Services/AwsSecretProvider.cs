using System.Collections.Concurrent;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace GAToolAPI.Services;

/// <summary>
///     Provides secrets from AWS Secrets Manager with startup preloading.
///     Secrets listed at startup are cached in memory for synchronous access.
/// </summary>
public class AwsSecretProvider : ISecretProvider
{
    private readonly IAmazonSecretsManager _client;
    private readonly ConcurrentDictionary<string, string> _cache;

    public AwsSecretProvider(IAmazonSecretsManager client, IDictionary<string, string> preloadedSecrets)
    {
        _client = client;
        _cache = new ConcurrentDictionary<string, string>(preloadedSecrets);
    }

    public string GetSecret(string name)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        throw new InvalidOperationException(
            $"Secret '{name}' was not preloaded at startup. Use GetSecretAsync() or add it to the preload list.");
    }

    public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        var response = await _client.GetSecretValueAsync(
            new GetSecretValueRequest { SecretId = name }, cancellationToken);
        var value = response.SecretString;
        _cache[name] = value;
        return value;
    }

    /// <summary>
    ///     Preloads a set of secrets from AWS Secrets Manager in parallel.
    /// </summary>
    public static async Task<IDictionary<string, string>> PreloadSecretsAsync(
        IAmazonSecretsManager client, IEnumerable<string> secretNames)
    {
        var secrets = new ConcurrentDictionary<string, string>();
        var tasks = secretNames.Select(async name =>
        {
            var response = await client.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = name });
            secrets[name] = response.SecretString;
        });
        await Task.WhenAll(tasks);
        return secrets;
    }
}
