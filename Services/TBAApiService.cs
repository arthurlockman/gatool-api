using System.Text.Json;
using GAToolAPI.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class TBAApiService
{
    private const string ServiceKey = "tba";

    private readonly HttpClient _httpClient;
    private readonly IFusionCache _cache;
    private readonly CacheTtlContext _ttlContext;
    private readonly JsonSerializerOptions _jsonOptions;

    public TBAApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext)
    {
        _httpClient = httpClient;
        _cache = cache;
        _ttlContext = ttlContext;
        _httpClient.BaseAddress = new Uri("https://www.thebluealliance.com/api/v3/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            "X-TBA-Auth-Key", secretProvider.GetSecret("TBAApiKey"));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null) =>
        CachedHttpGet.Get<T>(_cache, _ttlContext, ServiceKey, path, query, FetchTyped<T>);

    private async Task<T?> FetchTyped<T>(string path, IDictionary<string, string?>? query)
    {
        var requestUrl = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("The Blue Alliance", response.StatusCode, errorContent);
    }
}