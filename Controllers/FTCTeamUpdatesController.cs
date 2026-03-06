using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[Route("ftc/v2")]
[OpenApiTag("Community Updates")]
public class FtcTeamUpdatesController(UserStorageService userStorage, TeamDataService teamData) : ControllerBase
{
    /// <summary>
    ///     Gets community updates (pit display data) for an FTC team.
    /// </summary>
    /// <param name="teamNumber">The FTC team number.</param>
    /// <returns>Object with teamNumber and updates JSON, or teamNumber only if no updates.</returns>
    /// <response code="200">Returns the team updates or empty placeholder.</response>
    [HttpGet("team/{teamNumber}/updates")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdatesForTeam(string teamNumber)
    {
        var teamUpdate = await userStorage.GetTeamUpdates(teamNumber, true);
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

    /// <summary>
    ///     Gets the update history for an FTC team's community updates.
    /// </summary>
    /// <param name="teamNumber">The FTC team number.</param>
    /// <returns>Update history entries.</returns>
    /// <response code="200">Returns the update history.</response>
    [HttpGet("team/{teamNumber}/updates/history")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdateHistory(string teamNumber)
    {
        var updateHistory = await userStorage.GetTeamUpdateHistory(teamNumber, true);
        return Ok(updateHistory);
    }

    /// <summary>
    ///     Stores or updates community updates (pit display data) for an FTC team. Requires user authorization.
    /// </summary>
    /// <param name="updates">JSON object containing the updates to store.</param>
    /// <param name="teamNumber">The FTC team number.</param>
    /// <response code="204">Updates stored successfully.</response>
    /// <response code="400">Missing user email in token.</response>
    [HttpPut("team/{teamNumber}/updates")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [Authorize("user")]
    public async Task<IActionResult> StoreTeamUpdatesForTeam([FromBody] JsonObject updates, string teamNumber)
    {
        var email = User.FindFirst("name")?.Value;
        if (email == null) return BadRequest("Missing user email address in token");
        await userStorage.StoreTeamUpdates(teamNumber, updates, email, true);
        return NoContent();
    }

    /// <summary>
    ///     Gets community updates for all teams at an FTC event.
    /// </summary>
    /// <param name="year">The competition year/season.</param>
    /// <param name="eventCode">The event code.</param>
    /// <returns>Array of objects with teamNumber and updates for each team that has updates.</returns>
    /// <response code="200">Returns updates for teams at the event.</response>
    /// <response code="204">No team list or updates found.</response>
    [HttpGet("{year}/communityUpdates/{eventCode}")]
    [ProducesResponseType(typeof(JsonObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> GetTeamUpdatesForEvent(string year, string eventCode)
    {
        var teamList = await teamData.GetFtcTeamData(year, eventCode);
        if (teamList == null) return NoContent();

        var teamNumbers = teamList["teams"]?.AsArray().Select(t => t?["teamNumber"]?.ToString());
        if (teamNumbers == null) return NoContent();
        var tasks = teamNumbers.Select(async t =>
        {
            if (t == null) return null;
            var update = await userStorage.GetTeamUpdates(t, true);
            return string.IsNullOrWhiteSpace(update)
                ? null
                : new
                {
                    teamNumber = t,
                    updates = JsonSerializer.Deserialize<JsonObject>(update)
                };
        });
        var data = await Task.WhenAll(tasks);
        return Ok(data.Where(d => d != null));
    }
}