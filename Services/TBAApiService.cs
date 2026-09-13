using System.Text.Json;
using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class TBAApiService: ApiService
{
    public TBAApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext): base(httpClient, cache, ttlContext, "tba", new JsonSerializerOptions
    {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        })
    {
        _httpClient.BaseAddress = new Uri("https://www.thebluealliance.com/api/v3/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            "X-TBA-Auth-Key", secretProvider.GetSecret("TBAApiKey"));
    }
}