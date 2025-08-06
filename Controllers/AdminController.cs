using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[Route("/v3/system/admin")]
[OpenApiTag("Administration")]
public class AdminController(UserStorageService userStorage): ControllerBase
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
    public async Task<IActionResult> StoreEventAnnouncements([FromBody] JsonObject json, string eventCode)
    {
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
}