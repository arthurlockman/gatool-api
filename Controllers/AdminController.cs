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
    /// <summary>Lists roles that an administrator can manually grant or revoke. Requires admin authorization.</summary>
    /// <returns>Assignable role names and display metadata.</returns>
    /// <response code="200">Returns the assignable role catalog.</response>
    /// <response code="401">An access token is required.</response>
    /// <response code="403">The caller does not have the admin role.</response>
    [HttpGet("roles")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<AssignableRoleMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public IActionResult GetAssignableRoles()
    {
        return Ok(AuthRoleCatalog.ManuallyAssignable);
    }

    /// <summary>Searches users by email prefix. Requires admin authorization.</summary>
    /// <param name="query">Required, nonblank email prefix; trimmed and matched case-insensitively.</param>
    /// <param name="limit">Maximum results to return, from 1 to 50; defaults to 20.</param>
    /// <param name="cancellationToken">Request cancellation token; not a client-supplied parameter.</param>
    /// <returns>User summaries with email, roles, creation time, and last login time.</returns>
    /// <response code="200">Returns matching users, or an empty list when none match.</response>
    /// <response code="400">The query is blank or the limit is outside 1 to 50.</response>
    /// <response code="401">An access token is required.</response>
    /// <response code="403">The caller does not have the admin role.</response>
    [HttpGet("users")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<UserSummary>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> SearchUsers([FromQuery] string? query, [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return BadRequest(new { message = "query is required" });
        if (limit is < 1 or > 50) return BadRequest(new { message = "limit must be between 1 and 50" });

        var users = await authRepository.SearchUsersAsync(query, limit, cancellationToken);
        return Ok(users.Select(ToSummary));
    }

    /// <summary>Grants a manually assignable role to an existing user. Requires admin authorization.</summary>
    /// <param name="email">Email of the existing user, URL-encoded when necessary.</param>
    /// <param name="role">Role name from GET /v3/system/roles.</param>
    /// <param name="cancellationToken">Request cancellation token; not a client-supplied parameter.</param>
    /// <returns>The updated user summary.</returns>
    /// <response code="200">Returns the user with the role present.</response>
    /// <response code="400">The role is not manually assignable.</response>
    /// <response code="404">The user does not exist.</response>
    /// <response code="401">An access token is required.</response>
    /// <response code="403">The caller does not have the admin role.</response>
    [HttpPut("users/{email}/roles/{role}")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(UserSummary), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public Task<IActionResult> GrantRole(string email, string role, CancellationToken cancellationToken)
    {
        return SetRolePresence(email, role, true, cancellationToken);
    }

    /// <summary>Revokes a manually assignable role from an existing user. Requires admin authorization.</summary>
    /// <param name="email">Email of the existing user, URL-encoded when necessary.</param>
    /// <param name="role">Role name from GET /v3/system/roles.</param>
    /// <param name="cancellationToken">Request cancellation token; not a client-supplied parameter.</param>
    /// <returns>The updated user summary.</returns>
    /// <response code="200">Returns the user with the role absent.</response>
    /// <response code="400">The role is not manually assignable.</response>
    /// <response code="404">The user does not exist.</response>
    /// <response code="401">An access token is required.</response>
    /// <response code="403">The caller does not have the admin role.</response>
    [HttpDelete("users/{email}/roles/{role}")]
    [Authorize(AuthPolicies.Admin)]
    [ProducesResponseType(typeof(UserSummary), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public Task<IActionResult> RevokeRole(string email, string role, CancellationToken cancellationToken)
    {
        return SetRolePresence(email, role, false, cancellationToken);
    }

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
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
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

    private static UserSummary ToSummary(UserRecord user)
    {
        return new UserSummary(user.Email, user.Roles, user.CreatedAt, user.LastLoginAt);
    }
}