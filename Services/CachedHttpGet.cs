using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Services;

/// <summary>
///     Helpers that wrap downstream HTTP GETs through FusionCache so concurrent requests
///     for the same path are deduplicated (single-flight) and downstream APIs are
///     protected from cache-stampede traffic across the entire ECS fleet.
/// </summary>
/// <remarks>
///     The desired TTL for any given call is resolved from the request-scoped
///     <see cref="CacheTtlContext" />, which is populated by
///     <see cref="GAToolAPI.Attributes.RedisCacheAttribute" /> on the controller action.
///     If no TTL is set (e.g. an action without the attribute, or a background job), the cache
///     is bypassed entirely and the factory runs directly against the upstream API.
/// </remarks>
internal static class CachedHttpGet
{
    public static async Task<T?> Get<T>(
        IFusionCache cache,
        CacheTtlContext ttlContext,
        string serviceKey,
        string path,
        IDictionary<string, string?>? query,
        Func<string, IDictionary<string, string?>?, Task<T?>> factory)
    {
        if (ttlContext.Duration is not { } duration)
            return await factory(path, query);

        var cacheKey = BuildKey(serviceKey, path, query);
        return await cache.GetOrSetAsync<T?>(
            cacheKey,
            (_, _) => factory(path, query),
            BuildOptions(duration));
    }

    public static async Task<JsonObject?> GetGeneric(
        IFusionCache cache,
        CacheTtlContext ttlContext,
        string serviceKey,
        string path,
        IDictionary<string, string?>? query,
        Func<string, IDictionary<string, string?>?, Task<JsonObject?>> factory)
    {
        if (ttlContext.Duration is not { } duration)
            return await factory(path, query);

        var cacheKey = BuildKey(serviceKey, path, query);
        return await cache.GetOrSetAsync<JsonObject?>(
            cacheKey,
            (_, _) => factory(path, query),
            BuildOptions(duration));
    }

    public static async Task<JsonArray?> GetGenericArray(
        IFusionCache cache,
        CacheTtlContext ttlContext,
        string serviceKey,
        string path,
        IDictionary<string, string?>? query,
        Func<string, IDictionary<string, string?>?, Task<JsonArray?>> factory)
    {
        if (ttlContext.Duration is not { } duration)
            return await factory(path, query);

        var cacheKey = BuildKey(serviceKey, path, query);
        return await cache.GetOrSetAsync<JsonArray?>(
            cacheKey,
            (_, _) => factory(path, query),
            BuildOptions(duration));
    }

    private static FusionCacheEntryOptions BuildOptions(TimeSpan duration) =>
        new() { Duration = duration };

    private static string BuildKey(string serviceKey, string path, IDictionary<string, string?>? query)
    {
        var url = query != null ? QueryHelpers.AddQueryString(path, query) : path;
        return $"svc:{serviceKey}:{url}";
    }
}
