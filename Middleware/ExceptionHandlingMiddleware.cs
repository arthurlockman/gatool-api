using System.Net;
using System.Text.Json;
using GAToolAPI.Exceptions;

namespace GAToolAPI.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "An unhandled exception occurred");

        context.Response.ContentType = "application/json";

        HttpStatusCode statusCode;
        object response;

        if (exception is ExternalApiException externalApiException)
        {
            // Preserve the status code from the external API
            statusCode = externalApiException.StatusCode;

            response = new
            {
                error = new
                {
                    message = externalApiException.Message,
                    type = "ExternalApiError",
                    apiName = externalApiException.ApiName,
                    statusCode = (int)statusCode,
                    details = externalApiException.ResponseBody
                }
            };
        }
        else
        {
            statusCode = exception switch
            {
                ArgumentException => HttpStatusCode.BadRequest,
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                KeyNotFoundException => HttpStatusCode.NotFound,
                _ => HttpStatusCode.InternalServerError
            };

            response = new
            {
                error = new
                {
                    message = exception.Message,
                    type = exception.GetType().Name,
                    statusCode = (int)statusCode
                }
            };
        }

        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
