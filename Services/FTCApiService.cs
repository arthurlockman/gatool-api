using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class FTCApiService : ApiService
{
    public FTCApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext) : base(httpClient, cache, ttlContext, "ftc")
    {
        _httpClient.BaseAddress = new Uri("https://ftc-api.firstinspires.org/v2.0/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Authorization, secretProvider.GetSecret("FTCApiKey"));
    }
}