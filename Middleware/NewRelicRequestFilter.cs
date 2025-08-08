using NewRelic.Api.Agent;

namespace GAToolAPI.Middleware;

public class NewRelicRequestFilter(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // Check if the response is a 404 and ignore it for New Relic
        if (context.Response.StatusCode == 404)
        {
            // Tell New Relic to ignore this transaction
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();

            // Also ignore any errors that might be associated with this transaction
            NewRelic.Api.Agent.NewRelic.IgnoreApdex();
        }

        // Ignore livecheck and version endpoints from Apdex calculation
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (path is "/livecheck" or "/version")
        {
            NewRelic.Api.Agent.NewRelic.IgnoreApdex();
        }

        // Ignore any swagger-based requests entirely
        if (path != null && path.Contains("swagger"))
        {
            NewRelic.Api.Agent.NewRelic.IgnoreApdex();
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
        }
    }
}
