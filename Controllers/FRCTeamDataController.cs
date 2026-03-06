using GAToolAPI.Attributes;
using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[ApiController]
[Route("v3")]
public class FrcTeamDataController(TBAApiService tbaApi) : ControllerBase
{
    /// <summary>
    ///     Gets event appearances for an FRC team from The Blue Alliance (all events the team has attended).
    /// </summary>
    /// <param name="teamNumber">The FRC team number.</param>
    /// <returns>List of TBA event records for the team.</returns>
    /// <response code="200">Returns the list of event appearances.</response>
    [HttpGet("team/{teamNumber:int}/appearances")]
    [ProducesResponseType<List<RawTbaEvent>>(StatusCodes.Status200OK)]
    [RedisCache("frcapi:teamappearances", RedisCacheTime.ThreeDays)]
    [OpenApiTag("FRC Team Data")]
    public async Task<ActionResult> GetAppearances(int teamNumber)
    {
        var data = await tbaApi.Get<List<RawTbaEvent>>($"team/frc{teamNumber}/events");
        return Ok(data);
    }
}