using GAToolAPI.Models;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[OpenApiTag("Administration")]
public class LiveCheckController(IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>
    ///     Health check endpoint; returns a simple alive message. Used for load balancer or monitoring probes.
    /// </summary>
    /// <returns>Plain text "I'm alive!".</returns>
    /// <response code="200">Service is running.</response>
    [HttpGet("/livecheck")]
    [Produces("text/plain")]
    public IActionResult LiveCheck()
    {
        Response.Headers.CacheControl = "no-cache";
        return Ok("I'm alive!");
    }

    /// <summary>
    ///     Returns API version and environment information.
    /// </summary>
    /// <returns>Version info including environment name.</returns>
    /// <response code="200">Returns the version details.</response>
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