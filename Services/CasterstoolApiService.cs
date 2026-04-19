using System.Text.Json;
using GAToolAPI.Exceptions;
using GAToolAPI.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

public class CasterstoolApiService
{
    private const string ServiceKey = "casterstool";

    private readonly HttpClient _httpClient;
    private readonly IFusionCache _cache;
    private readonly CacheTtlContext _ttlContext;
    private readonly JsonSerializerOptions _jsonOptions;

    public CasterstoolApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext)
    {
        _httpClient = httpClient;
        _cache = cache;
        _ttlContext = ttlContext;
        _httpClient.BaseAddress = new Uri("https://casterstool.com/api/");
        _httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", secretProvider.GetSecret("CasterstoolApiKey"));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public Task<List<TeamConnection>?> GetConnections(string year, string eventCode, string teamNumbers)
    {
        var path = $"events/{year}{eventCode}/summary/connections";
        var query = new Dictionary<string, string?>
        {
            ["teams"] = teamNumbers,
            ["all_time"] = "true"
        };

        return CachedHttpGet.Get<List<TeamConnection>>(
            _cache, _ttlContext, ServiceKey, path, query,
            (p, q) => FetchConnections(p, q!));
    }

    private async Task<List<TeamConnection>?> FetchConnections(string path, IDictionary<string, string?> query)
    {
        var requestUrl = QueryHelpers.AddQueryString(path, query);
        var response = await _httpClient.GetAsync(requestUrl);

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<List<TeamConnection>>(_jsonOptions);

        var errorContent = await response.Content.ReadAsStringAsync();
        throw new ExternalApiException("Casterstool", response.StatusCode, errorContent);
    }
}
