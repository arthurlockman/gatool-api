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
    [HttpGet("team/{teamNumber:int}/updates")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdatesForTeam(int teamNumber)
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

    [HttpGet("team/{teamNumber:int}/updates/history")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdateHistory(int teamNumber)
    {
        var updateHistory = await userStorage.GetTeamUpdateHistory(teamNumber);
        return Ok(updateHistory);
    }

    [HttpPut("team/{teamNumber:int}/updates")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("user")]
    public async Task<IActionResult> StoreTeamUpdatesForTeam([FromBody] JsonObject updates, int teamNumber)
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
        var teamNumbers = teamList?.Teams?.Select(t => t.TeamNumber);
        if (teamNumbers == null) return NoContent();
        var tasks = teamNumbers.Select(async t =>
        {
            var update = await userStorage.GetTeamUpdates((int)t);
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