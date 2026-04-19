using System.Net;
using System.Text.Json;
using GAToolAPI.Exceptions;
using BadHttpRequestException = Microsoft.AspNetCore.Http.BadHttpRequestException;

namespace GAToolAPI.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an error, just log at debug level
            logger.LogDebug("Request cancelled by client: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Microsoft.AspNetCore.Connections.ConnectionResetException)
        {
            logger.LogDebug("Connection reset by client: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex) when (ex is IOException && context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request body read aborted by client: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (BadHttpRequestException ex)
        {
            // Malformed request, slow upload, oversized body, etc. — caller's fault, not ours.
            // Log at Warning (not Error) so it doesn't pollute alerts, and respond 400.
            logger.LogWarning(
                "Bad HTTP request: {Method} {Path} — {Reason}",
                context.Request.Method, context.Request.Path, ex.Message);

            if (!context.Response.HasStarted)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = ex.StatusCode == 0
                    ? (int)HttpStatusCode.BadRequest
                    : ex.StatusCode;

                var response = new
                {
                    error = new
                    {
                        message = ex.Message,
                        type = nameof(BadHttpRequestException),
                        statusCode = context.Response.StatusCode
                    }
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonSerializerOptions));
            }
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

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonSerializerOptions));
    }
}