using System.Net;

namespace GAToolAPI.Exceptions;

public class ExternalApiException(
    string apiName,
    HttpStatusCode statusCode,
    string? responseBody = null,
    string? message = null)
    : Exception(message ?? $"{apiName} returned {(int)statusCode}: {statusCode}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
    public string ApiName { get; } = apiName;
}
