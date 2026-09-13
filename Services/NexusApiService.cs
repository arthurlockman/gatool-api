using Microsoft.Net.Http.Headers;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

public class NexusApiService : ApiService
{
    public NexusApiService(HttpClient httpClient, ISecretProvider secretProvider, IFusionCache cache,
        CacheTtlContext ttlContext) : base(httpClient, cache, ttlContext, "nexus")
    {
        _httpClient.BaseAddress = new Uri("https://frc.nexus/api/v1/event/");
        _httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
        // FRC Nexus API authentication — see https://frc.nexus/api
        _httpClient.DefaultRequestHeaders.Add("Nexus-Api-Key", secretProvider.GetSecret("NexusApiKey"));
    }
}