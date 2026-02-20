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
public class AdminController(UserStorageService userStorage, IConnectionMultiplexer redis, ILogger<AdminController> logger) : ControllerBase
{
    [HttpPut("announcements")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("admin")]
    public async Task<IActionResult> StoreAnnouncements([FromBody] JsonObject json)
    {
        await userStorage.StoreGlobalAnnouncements(json);
        return NoContent();
    }

    [HttpPut("announcements/{eventCode}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("admin")]
    [Authorize("user")]
    public async Task<IActionResult> StoreEventAnnouncements([FromBody] JsonNode body, string eventCode)
    {
        var json = body?.ToJsonString() ?? "[]";
        await userStorage.StoreEventAnnouncements(eventCode, json);
        return NoContent();
    }

    [HttpGet("syncusers")]
    [Authorize("admin")]
    public async Task<IActionResult> GetUserSyncStatus()
    {
        var syncResults = await userStorage.GetUserSyncResults();
        if (syncResults != null) return Ok(JsonSerializer.Deserialize<JsonObject>(syncResults));
        return NoContent();
    }

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