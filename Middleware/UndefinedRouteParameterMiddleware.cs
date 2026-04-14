using System.Text.Json;

namespace GAToolAPI.Middleware;

/// <summary>
///     Rejects requests containing literal "undefined" or "null" path segments,
///     which typically originate from uninitialized JavaScript variables on the frontend.
/// </summary>
public class UndefinedRouteParameterMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> InvalidSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "undefined",
        "null",
        "NaN",
        "[object Object]"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (path != null && HasInvalidSegment(path))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    message = "Request path contains an uninitialized parameter value.",
                    type = "InvalidRouteParameter",
                    statusCode = 400,
                    path
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
            return;
        }

        await next(context);
    }

    private static bool HasInvalidSegment(string path)
    {
        // Check for empty segments (double slashes from empty string variables)
        if (path.Contains("//"))
            return true;

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (InvalidSegments.Contains(segment))
                return true;
        }

        return false;
    }
}
