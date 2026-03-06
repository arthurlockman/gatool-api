using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using StackExchange.Redis;

namespace GAToolAPI.Controllers;

[Route("/v3/system")]
[OpenApiTag("Administration")]
public class AdminController(
    UserStorageService userStorage,
    IConnectionMultiplexer redis,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    ///     Stores or updates global system announcements. Requires admin authorization.
    /// </summary>
    /// <param name="json">JSON object containing the announcements.</param>
    /// <response code="204">Announcements stored successfully.</response>
    [HttpPut("announcements")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("admin")]
    public async Task<IActionResult> StoreAnnouncements([FromBody] JsonObject json)
    {
        await userStorage.StoreGlobalAnnouncements(json);
        return NoContent();
    }

    /// <summary>
    ///     Stores or updates announcements for a specific event. Requires user authorization.
    /// </summary>
    /// <param name="body">JSON body containing the event announcements.</param>
    /// <param name="eventCode">The event code.</param>
    /// <response code="204">Announcements stored successfully.</response>
    [HttpPut("announcements/{eventCode}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("user")]
    public async Task<IActionResult> StoreEventAnnouncements([FromBody] JsonNode? body, string eventCode)
    {
        var json = body?.ToJsonString() ?? "[]";
        await userStorage.StoreEventAnnouncements(eventCode, json);
        return NoContent();
    }

    /// <summary>
    ///     Gets the status/results of the last user sync operation. Requires admin authorization.
    /// </summary>
    /// <returns>Sync results as JSON.</returns>
    /// <response code="200">Returns the sync status.</response>
    /// <response code="204">No sync data found.</response>
    [HttpGet("syncusers")]
    [Authorize("admin")]
    public async Task<IActionResult> GetUserSyncStatus()
    {
        var syncResults = await userStorage.GetUserSyncResults();
        if (syncResults != null) return Ok(JsonSerializer.Deserialize<JsonObject>(syncResults));
        return NoContent();
    }

    /// <summary>
    ///     Clears all entries in the Redis cache. Requires admin authorization.
    /// </summary>
    /// <response code="204">Cache cleared successfully.</response>
    /// <response code="500">Failed to clear cache.</response>
    [HttpDelete("cache")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    [Authorize("admin")]
    public async Task<IActionResult> ClearRedisCache()
    {
        try
        {
            logger.LogInformation("Admin user requesting Redis cache clear");

            var database = redis.GetDatabase();
            var server = redis.GetServer(redis.GetEndPoints().First());

            await server.FlushDatabaseAsync(database.Database);

            logger.LogInformation("Redis cache cleared successfully");
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear Redis cache");
            return StatusCode(500, new { message = "Failed to clear Redis cache", error = ex.Message });
        }
    }
}