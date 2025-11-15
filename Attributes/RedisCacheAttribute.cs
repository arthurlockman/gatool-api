using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StackExchange.Redis;

namespace GAToolAPI.Attributes;

/// <summary>
/// Static class that provides functionality to ignore caching for the current request
/// </summary>
public static class RedisCache
{
    private const string IgnoreCacheKey = "RedisCache_IgnoreCurrentRequest";

    /// <summary>
    /// Call this method within a controller action to prevent the response from being cached
    /// </summary>
    /// <param name="httpContext">The current HttpContext (optional - will use current context if null)</param>
    public static void IgnoreCurrentRequest(HttpContext? httpContext = null)
    {
        httpContext ??= GetCurrentHttpContext();
        if (httpContext != null)
        {
            httpContext.Items[IgnoreCacheKey] = true;
        }
    }

    /// <summary>
    /// Checks if the current request should be ignored for caching
    /// </summary>
    /// <param name="httpContext">The HttpContext to check</param>
    /// <returns>True if caching should be ignored, false otherwise</returns>
    internal static bool ShouldIgnoreCurrentRequest(HttpContext httpContext)
    {
        return httpContext.Items.ContainsKey(IgnoreCacheKey) &&
               httpContext.Items[IgnoreCacheKey] is bool ignore && ignore;
    }

    private static HttpContext? GetCurrentHttpContext()
    {
        var httpContextAccessor = ServiceLocator.ServiceProvider?.GetService<IHttpContextAccessor>();
        return httpContextAccessor?.HttpContext;
    }
}

/// <summary>
/// Simple service locator to access HttpContext when not directly available
/// </summary>
public static class ServiceLocator
{
    public static IServiceProvider? ServiceProvider { get; set; }
}

[AttributeUsage(AttributeTargets.Method)]
public class RedisCacheAttribute(string keyPrefix, int durationMinutes = 60) : ActionFilterAttribute
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var redis = context.HttpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

        var cacheKey = BuildCacheKey(context);

        // Check if caching is ignored for this request
        if (!RedisCache.ShouldIgnoreCurrentRequest(context.HttpContext))
        {
            var cachedResult = await redis.StringGetAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedResult))
            {
                var cachedObject = JsonSerializer.Deserialize<object>(cachedResult!);
                context.Result = new OkObjectResult(cachedObject);
                return;
            }
        }

        var executedContext = await next();

        // Only cache if not ignored and result is valid
        if (!RedisCache.ShouldIgnoreCurrentRequest(context.HttpContext) &&
            executedContext.Result is OkObjectResult { Value: not null } okResult)
        {
            var serializedResult = JsonSerializer.Serialize(okResult.Value, _jsonOptions);
            await redis.StringSetAsync(cacheKey, serializedResult, TimeSpan.FromMinutes(durationMinutes));
        }
    }

    private string BuildCacheKey(ActionExecutingContext context)
    {
        var keyParts = new List<string> { keyPrefix };
        keyParts.AddRange(context.RouteData.Values.Select(routeValue => $"net:{routeValue.Key}:{routeValue.Value}"));
        keyParts.AddRange(from queryParam in context.HttpContext.Request.Query
            where !string.IsNullOrEmpty(queryParam.Value)
            select $"{queryParam.Key}:{queryParam.Value}");
        return string.Join(":", keyParts);
    }
}

public static class RedisCacheTime
{
    public const int OneMinute = 1;
    public const int FiveMinutes = OneMinute * 5;
    public const int ThirtyMinutes = OneMinute * 30;
    public const int OneHour = OneMinute * 60;
    public const int OneDay = OneHour * 24;
    public const int ThreeDays = OneDay * 3;
    public const int OneWeek = OneDay * 7;
    public const int OneMonth = OneDay * 30;
}