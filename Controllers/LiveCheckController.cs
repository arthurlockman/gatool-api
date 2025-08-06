using GAToolAPI.Models;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[OpenApiTag("Administration")]
public class LiveCheckController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("/livecheck")]
    [Produces("text/plain")]
    public IActionResult LiveCheck()
    {
        Response.Headers.CacheControl = "no-cache";
        return Ok("I'm alive!");
    }

    [HttpGet("/version")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(VersionResponse), 200)]
    public IActionResult VersionCheck()
    {
        Response.Headers.CacheControl = "no-cache";
        var versionInfo = VersionInfo.GetVersionInfo(environment.EnvironmentName);
        return Ok(versionInfo);
    }
}