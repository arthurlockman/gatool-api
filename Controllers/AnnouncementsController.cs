using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[ApiController]
[Route("v3/announcements")]
[OpenApiTag("System Announcements")]
public class AnnouncementsController(UserStorageService userStorage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetAnnouncements()
    {
        var data = await userStorage.GetGlobalAnnouncements();
        if (data == null) return NoContent();
        var json = JsonSerializer.Deserialize<JsonObject>(data);
        return Ok(json);
    }

    [HttpGet("{eventCode}")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEventAnnouncements(string eventCode)
    {
        var data = await userStorage.GetEventAnnouncements(eventCode);
        if (data == null) return NoContent();
        var json = JsonSerializer.Deserialize<JsonObject>(data);
        return Ok(json);
    }
}