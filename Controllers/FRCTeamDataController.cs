using GAToolAPI.Models;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[ApiController]
[Microsoft.AspNetCore.Components.Route("v3")]
public class FrcTeamDataController(TBAApiService tbaApi): ControllerBase
{
    [HttpGet("/team/{teamNumber:int}/appearances")]
    [ProducesResponseType<List<RawTbaEvent>>(StatusCodes.Status200OK)]
    [OpenApiTag("FRC Team Data")]
    public async Task<ActionResult> GetAppearances(int teamNumber)
    {
        var data = await tbaApi.Get<List<RawTbaEvent>>($"team/frc{teamNumber}/events");
        return Ok(data);
    }
}