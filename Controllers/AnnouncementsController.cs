using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[ApiController]
[Route("v3/announcements")]
[OpenApiTag("System Announcements")]
public class AnnouncementsController(UserStorageService userStorage) : ControllerBase
{
    /// <summary>
    ///     Gets global system announcements.
    /// </summary>
    /// <returns>Announcements as JSON.</returns>
    /// <response code="200">Returns the announcements.</response>
    /// <response code="204">No announcements found.</response>
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

    /// <summary>
    ///     Gets announcements for a specific event.
    /// </summary>
    /// <param name="eventCode">The event code.</param>
    /// <returns>Event announcements as JSON.</returns>
    /// <response code="200">Returns the event announcements.</response>
    /// <response code="204">No announcements found.</response>
    [HttpGet("{eventCode}")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetEventAnnouncements(string eventCode)
    {
        var data = await userStorage.GetEventAnnouncements(eventCode);
        if (data == null) return NoContent();
        return Ok(data);
    }
}