using Microsoft.AspNetCore.Mvc;

namespace GPMH.WebAPI.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    [HttpGet("/api/v1/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetLiveness() => Ok(new
    {
        status = "healthy",
        service = "GPMH.WebAPI",
        timestampUtc = DateTimeOffset.UtcNow,
    });
}

