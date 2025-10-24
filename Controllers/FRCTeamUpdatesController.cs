using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[Route("v3/")]
[OpenApiTag("Community Updates")]
public class FrcTeamUpdatesController(UserStorageService userStorage, TeamDataService teamData) : ControllerBase
{
    [HttpGet("team/{teamNumber}/updates")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdatesForTeam(string teamNumber)
    {
        var teamUpdate = await userStorage.GetTeamUpdates(teamNumber);
        if (teamUpdate == null)
            return Ok(new
            {
                teamNumber
            });

        return Ok(new
        {
            teamNumber,
            updates = JsonSerializer.Deserialize<JsonObject>(teamUpdate)
        });
    }

    [HttpGet("team/{teamNumber}/updates/history")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdateHistory(string teamNumber)
    {
        var updateHistory = await userStorage.GetTeamUpdateHistory(teamNumber);
        return Ok(updateHistory);
    }

    [HttpPut("team/{teamNumber}/updates")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("user")]
    public async Task<IActionResult> StoreTeamUpdatesForTeam([FromBody] JsonObject updates, string teamNumber)
    {
        var email = User.FindFirst("name")?.Value;
        if (email == null) return BadRequest("Missing user email address in token");
        await userStorage.StoreTeamUpdates(teamNumber, updates, email);
        return NoContent();
    }

    [HttpGet("{year}/communityUpdates/{eventCode}")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdatesForEvent(string year, string eventCode)
    {
        var teamList = await teamData.GetFrcTeamData(year, eventCode);
        var teamNumbers = teamList?.Teams?.Select(t => t.TeamNumber.ToString());
        if (teamNumbers == null) return NoContent();
        var tasks = teamNumbers.Select(async t =>
        {
            var update = await userStorage.GetTeamUpdates(t);
            return string.IsNullOrWhiteSpace(update) ? null : new
            {
                teamNumber = t,
                updates = JsonSerializer.Deserialize<JsonObject>(update)
            };
        });
        var data = await Task.WhenAll(tasks);
        return Ok(data.Where(d => d != null));
    }
}