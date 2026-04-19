using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Attributes;

/// <summary>
///     Static class that provides functionality to ignore caching for the current request
/// </summary>
public static class RedisCache
{
    private const string IgnoreCacheKey = "RedisCache_IgnoreCurrentRequest";

    /// <summary>
    ///     Call this method within a controller action to prevent the response from being cached
    /// </summary>
    /// <param name="httpContext">The current HttpContext (optional - will use current context if null)</param>
    public static void IgnoreCurrentRequest(HttpContext? httpContext = null)
    {
        httpContext ??= GetCurrentHttpContext();
        httpContext?.Items[IgnoreCacheKey] = true;
    }

    /// <summary>
    ///     Checks if the current request should be ignored for caching
    /// </summary>
    /// <param name="httpContext">The HttpContext to check</param>
    /// <returns>True if caching should be ignored, false otherwise</returns>
    internal static bool ShouldIgnoreCurrentRequest(HttpContext httpContext)
    {
        return httpContext.Items.ContainsKey(IgnoreCacheKey) &&
               httpContext.Items[IgnoreCacheKey] is true;
    }

    private static HttpContext? GetCurrentHttpContext()
    {
        var httpContextAccessor = ServiceLocator.ServiceProvider?.GetService<IHttpContextAccessor>();
        return httpContextAccessor?.HttpContext;
    }
}

/// <summary>
///     Simple service locator to access HttpContext when not directly available
/// </summary>
public static class ServiceLocator
{
    public static IServiceProvider? ServiceProvider { get; set; }
}

/// <summary>
///     Caches successful <see cref="OkObjectResult" /> responses for the configured duration.
///     Backed by FusionCache so all per-endpoint TTLs participate in the same L1 + L2 + backplane
///     setup as service-layer caching: stampede protection, fail-safe stale fallback, and
///     cross-task coherence are all inherited automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RedisCacheAttribute(string keyPrefix, int durationMinutes = 60) : ActionFilterAttribute
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Honor explicit opt-out: bypass cache entirely (read and write).
        if (RedisCache.ShouldIgnoreCurrentRequest(context.HttpContext))
        {
            await next();
            return;
        }

        // Signal the desired downstream cache TTL to any service called during this request.
        // This is what enables service-layer FusionCache writes — without this attribute the
        // request-scoped CacheTtlContext.Duration stays null and CachedHttpGet bypasses cache.
        var ttlContext = context.HttpContext.RequestServices.GetRequiredService<Services.CacheTtlContext>();
        ttlContext.Duration = TimeSpan.FromMinutes(durationMinutes);

        var cache = context.HttpContext.RequestServices.GetRequiredService<IFusionCache>();
        var cacheKey = BuildCacheKey(context);

        var entryOptions = new FusionCacheEntryOptions
        {
            Duration = TimeSpan.FromMinutes(durationMinutes),
            IsFailSafeEnabled = true,
            FailSafeMaxDuration = TimeSpan.FromDays(1),
            FailSafeThrottleDuration = TimeSpan.FromSeconds(30),
            // EagerRefreshThreshold is intentionally NOT set at the response layer.
            // Eager refresh would fire the captured factory in a background task after
            // the request scope has been disposed; the factory invokes next() on the
            // MVC pipeline, which depends on scoped services that are no longer
            // available. The service layer (CachedHttpGet) still benefits from the
            // global EagerRefreshThreshold — the response cache simply refreshes
            // synchronously on real expiry.
            AllowBackgroundDistributedCacheOperations = true,
            AllowBackgroundBackplaneOperations = true
        };

        ActionExecutedContext? executedCtx = null;

        var cachedJson = await cache.GetOrSetAsync<string?>(
            cacheKey,
            async (ctx, _) =>
            {
                executedCtx = await next();

                // If the action invoked RedisCache.IgnoreCurrentRequest() (e.g. empty result),
                // skip persisting to L1 + L2 so we don't poison the cache.
                if (RedisCache.ShouldIgnoreCurrentRequest(context.HttpContext))
                {
                    ctx.Options.SetSkipMemoryCache();
                    ctx.Options.SetSkipDistributedCache(true, true);
                    return null;
                }

                if (executedCtx.Result is OkObjectResult { Value: not null } ok)
                    return JsonSerializer.Serialize(ok.Value, JsonOptions);

                // Non-OK result: don't cache, but FusionCache requires we return something.
                ctx.Options.SetSkipMemoryCache();
                ctx.Options.SetSkipDistributedCache(true, true);
                return null;
            },
            entryOptions);

        // If the factory ran, the inner pipeline already populated context.Result via next().
        if (executedCtx != null) return;

        // Cache hit: short-circuit the action and replay the serialized OK payload.
        if (!string.IsNullOrEmpty(cachedJson))
            context.Result = new OkObjectResult(JsonSerializer.Deserialize<object>(cachedJson, JsonOptions));
    }

    private string BuildCacheKey(ActionExecutingContext context)
    {
        var keyParts = new List<string> { keyPrefix };
        keyParts.AddRange(context.RouteData.Values.Select(routeValue => $"net:{routeValue.Key}:{routeValue.Value}"));
        keyParts.AddRange(from queryParam in context.HttpContext.Request.Query
            where !string.IsNullOrEmpty(queryParam.Value)
            select $"{queryParam.Key}:{queryParam.Value}");

        // Include POST body data in cache key for POST/PUT/PATCH requests
        if (context.HttpContext.Request.Method is not ("POST" or "PUT" or "PATCH")) return string.Join(":", keyParts);
        // Find parameters marked with [FromBody] attribute
        var fromBodyParams = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource?.CanAcceptDataFrom(BindingSource.Body) == true)
            .Select(p => p.Name)
            .Where(name => context.ActionArguments.ContainsKey(name) && context.ActionArguments[name] != null)
            .Select(name => context.ActionArguments[name]!)
            .ToList();

        if (fromBodyParams.Count <= 0) return string.Join(":", keyParts);
        // Serialize body parameters and create a hash for the cache key
        // Sort lists within objects to ensure consistent hashing regardless of order
        var bodyJson = JsonSerializer.Serialize(fromBodyParams, JsonOptions);
        var bodyHash = ComputeHash(bodyJson);
        keyParts.Add($"body:{bodyHash}");

        return string.Join(":", keyParts);
    }

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant()[..16]; // Use first 16 chars of hash
    }
}

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class RedisCacheTime
{
    public const int OneMinute = 1;
    public const int FiveMinutes = OneMinute * 5;
    public const int OneHour = OneMinute * 60;
    public const int OneDay = OneHour * 24;
    public const int ThreeDays = OneDay * 3;
    public const int OneWeek = OneDay * 7;
    public const int OneMonth = OneDay * 30;
}