using System.Text.Json;
using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class TOAApiService : ApiService
{
    public TOAApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext) : base(httpClient, cache, ttlContext, "toa", new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    })
    {
        _httpClient.BaseAddress = new Uri("https://theorangealliance.org/api/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            "X-TOA-Key", secretProvider.GetSecret("TOAApiKey"));
        _httpClient.DefaultRequestHeaders.Add(
            "X-Application-Origin", secretProvider.GetSecret("TOAApiKey"));
    }
}