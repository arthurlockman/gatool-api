using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.AuthExtensions;
using GAToolAPI.Models;
using GAToolAPI.Services;
using GAToolAPI.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;

namespace GAToolAPI.Controllers;

[Route("/v3/system")]
[OpenApiTag("Administration")]
public class AdminController(
    UserStorageService userStorage,
    IConnectionMultiplexer redis,
    IFusionCache fusionCache,
    AuthRepository authRepository,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("roles")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<AssignableRoleMetadata>), (int)HttpStatusCode.OK)]
    public IActionResult GetAssignableRoles() => Ok(AuthRoleCatalog.ManuallyAssignable);

    [HttpGet("users")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<UserSummary>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> SearchUsers([FromQuery] string? query, [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return BadRequest(new { message = "query is required" });
        if (limit is < 1 or > 50) return BadRequest(new { message = "limit must be between 1 and 50" });

        var users = await authRepository.SearchUsersAsync(query, limit, cancellationToken);
        return Ok(users.Select(ToSummary));
    }

    [HttpPut("users/{email}/roles/{role}")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(UserSummary), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public Task<IActionResult> GrantRole(string email, string role, CancellationToken cancellationToken) =>
        SetRolePresence(email, role, true, cancellationToken);

    [HttpDelete("users/{email}/roles/{role}")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(UserSummary), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public Task<IActionResult> RevokeRole(string email, string role, CancellationToken cancellationToken) =>
        SetRolePresence(email, role, false, cancellationToken);

    /// <summary>
    ///     Stores or updates global system announcements. Requires admin authorization.
    /// </summary>
    /// <param name="json">JSON object containing the announcements.</param>
    /// <response code="204">Announcements stored successfully.</response>
    [HttpPut("announcements")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize(AuthPolicies.Admin)]
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
    [Authorize(AuthPolicies.User)]
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
    [Authorize(AuthPolicies.Admin)]
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
    [Authorize(AuthPolicies.Admin)]
    public async Task<IActionResult> ClearRedisCache()
    {
        try
        {
            logger.LogInformation("Admin user requesting Redis cache clear");

            var database = redis.GetDatabase();
            var server = redis.GetServer(redis.GetEndPoints().First());

            // Flush L2 (shared Redis) ...
            await server.FlushDatabaseAsync(database.Database);
            // ... and clear FusionCache's per-task L1 across the fleet via the backplane.
            await fusionCache.ClearAsync();

            logger.LogInformation("Redis cache cleared successfully");
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear Redis cache");
            return StatusCode(500, new { message = "Failed to clear Redis cache", error = ex.Message });
        }
    }

    private async Task<IActionResult> SetRolePresence(string email, string role, bool enabled,
        CancellationToken cancellationToken)
    {
        if (!AuthRoleCatalog.TryGetManuallyAssignable(role, out var roleMetadata))
            return BadRequest(new { message = $"Role '{role}' is not manually assignable" });

        var user = await authRepository.SetRolePresenceAsync(
            email, roleMetadata.Name, enabled, cancellationToken);
        return user == null ? NotFound() : Ok(ToSummary(user));
    }

    private static UserSummary ToSummary(UserRecord user) =>
        new(user.Email, user.Roles, user.CreatedAt, user.LastLoginAt);
}
