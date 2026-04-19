using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class FTCScoutApiService : IApiService
{
    private const string ServiceKey = "ftcscout";

    private readonly HttpClient _httpClient;
    private readonly IFusionCache _cache;
    private readonly CacheTtlContext _ttlContext;
    private readonly JsonSerializerOptions _jsonOptions;

    public FTCScoutApiService(HttpClient httpClient, IFusionCache cache, CacheTtlContext ttlContext)
    {
        _httpClient = httpClient;
        _cache = cache;
        _ttlContext = ttlContext;
        _httpClient.BaseAddress = new Uri("https://api.ftcscout.org/rest/v1/");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public Task<JsonObject?> GetGeneric(string path, IDictionary<string, string?>? query = null) =>
        CachedHttpGet.GetGeneric(_cache, _ttlContext, ServiceKey, path, query, FetchGeneric);

    public Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null) =>
        CachedHttpGet.Get<T>(_cache, _ttlContext, ServiceKey, path, query, FetchTyped<T>);

    private async Task<JsonObject?> FetchGeneric(string path, IDictionary<string, string?>? query)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<JsonObject>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("FTC Scout", response.StatusCode, errorContent);
    }

    private async Task<T?> FetchTyped<T>(string path, IDictionary<string, string?>? query)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("FTC Scout", response.StatusCode, errorContent);
    }
}