using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[ApiController]
[Route("v3/user/preferences")]
[OpenApiTag("User Preferences Data")]
public class UserDataController(UserStorageService userStorage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("user")]
    public async Task<IActionResult> GetUserData()
    {
        Response.Headers.CacheControl = "no-cache";
        var email = User.FindFirst("name")?.Value;
        if (email == null) return NoContent();
        var data = await userStorage.GetUserPreferences(email);
        if (data == null) return NoContent();
        var json = JsonSerializer.Deserialize<JsonObject>(data);
        return Ok(json);
    }

    [HttpPut]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("user")]
    public async Task<IActionResult> StoreUserPreferences([FromBody] JsonObject preferences)
    {
        var email = User.FindFirst("name")?.Value;
        if (email == null) return NoContent();
        await userStorage.StoreUserPreferences(email, preferences);
        return NoContent();
    }
}