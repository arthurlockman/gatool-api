using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

/// <summary>
///     Client for the FIRST Global Challenge API (https://api.first.global/v1).
/// </summary>
public class FirstGlobalApiService : IApiService
{
    private const string BaseAddress = "https://api.first.global/v1/";
    private const string ServiceKey = "firstglobal";

    private readonly HttpClient _httpClient;
    private readonly IFusionCache _cache;
    private readonly CacheTtlContext _ttlContext;
    private readonly JsonSerializerOptions _jsonOptions;

    public FirstGlobalApiService(HttpClient httpClient, IFusionCache cache, CacheTtlContext ttlContext)
    {
        _httpClient = httpClient;
        _cache = cache;
        _ttlContext = ttlContext;
        _httpClient.BaseAddress = new Uri(BaseAddress);
        _httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    ///     Gets the path prefix for the season (e.g. "2025" or empty for current season).
    /// </summary>
    public static string SeasonPath(string? year)
    {
        if (string.IsNullOrEmpty(year))
            return DateTime.Now.Year.ToString();
        return year;
    }

    public Task<JsonObject?> GetGeneric(string path, IDictionary<string, string?>? query = null) =>
        CachedHttpGet.GetGeneric(_cache, _ttlContext, ServiceKey, path, query, FetchGeneric);

    public Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null) =>
        CachedHttpGet.Get<T>(_cache, _ttlContext, ServiceKey, path, query, FetchTyped<T>);

    private async Task<JsonObject?> FetchGeneric(string path, IDictionary<string, string?>? query)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<JsonObject>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("FIRST Global API", response.StatusCode, errorContent);
    }

    private async Task<T?> FetchTyped<T>(string path, IDictionary<string, string?>? query)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("FIRST Global API", response.StatusCode, errorContent);
    }
}
