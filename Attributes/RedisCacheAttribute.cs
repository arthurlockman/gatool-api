using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StackExchange.Redis;

namespace GAToolAPI.Attributes;

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

        var cachedResult = await redis.StringGetAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedResult))
        {
            var cachedObject = JsonSerializer.Deserialize<object>(cachedResult!);
            context.Result = new OkObjectResult(cachedObject);
            return;
        }

        var executedContext = await next();
        if (executedContext.Result is OkObjectResult { Value: not null } okResult)
        {
            var serializedResult = JsonSerializer.Serialize(okResult.Value, _jsonOptions);
            await redis.StringSetAsync(cacheKey, serializedResult, TimeSpan.FromMinutes(durationMinutes));
        }
    }

    private string BuildCacheKey(ActionExecutingContext context)
    {
        var keyParts = new List<string> { keyPrefix };
        keyParts.AddRange(context.RouteData.Values.Select(routeValue => $"{routeValue.Key}:{routeValue.Value}"));
        keyParts.AddRange(from queryParam in context.HttpContext.Request.Query
            where !string.IsNullOrEmpty(queryParam.Value)
            select $"{queryParam.Key}:{queryParam.Value}");
        return string.Join(":", keyParts);
    }
}