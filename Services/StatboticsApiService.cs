using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

public class StatboticsApiService : ApiService
{
    public StatboticsApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext) : base(httpClient, cache, ttlContext, "statbotics")
    {
        _httpClient.BaseAddress = new Uri("https://api.statbotics.io/v3/");
    }
}