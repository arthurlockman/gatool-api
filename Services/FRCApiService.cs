using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

// ReSharper disable once InconsistentNaming
public class FRCApiService : ApiService
{
    public FRCApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext) : base(httpClient, cache, ttlContext, "frc")
    {
        _httpClient.BaseAddress = new Uri("https://frc-api.firstinspires.org/v3.0/");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Accept, "application/json");
        _httpClient.DefaultRequestHeaders.Add(
            HeaderNames.Authorization, secretProvider.GetSecret("FRCApiKey"));
    }
}