using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public abstract class ApiService(
    HttpClient httpClient,
    IFusionCache cache,
    CacheTtlContext ttlContext,
    string serviceKey,
    JsonSerializerOptions jsonOptions) : IApiService
{
    protected readonly HttpClient _httpClient = httpClient;

    protected ApiService(HttpClient httpClient, IFusionCache cache, CacheTtlContext ttlContext, string serviceKey) :
        this(httpClient, cache, ttlContext, serviceKey, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })
    {
        // nothing to do here
    }

    public Task<JsonObject?> GetGeneric(string path, IDictionary<string, string?>? query = null)
    {
        return CachedHttpGet.GetGeneric(cache, ttlContext, serviceKey, path, query, FetchGeneric);
    }

    public Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null)
    {
        return CachedHttpGet.Get(cache, ttlContext, serviceKey, path, query, FetchTyped<T>);
    }

    private async Task<JsonObject?> FetchGeneric(string path, IDictionary<string, string?>? query)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(jsonString)
                ? default
                : JsonSerializer.Deserialize<JsonObject>(jsonString, jsonOptions);
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException(serviceKey, response.StatusCode, errorContent);
    }

    private async Task<T?> FetchTyped<T>(string path, IDictionary<string, string?>? query)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(jsonString)
                ? default
                : JsonSerializer.Deserialize<T>(jsonString, jsonOptions);
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException(serviceKey, response.StatusCode, errorContent);
    }
}