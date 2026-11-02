using Chubb.Claims.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chubb.Claims.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(IDatabaseHealth database) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "live" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var ready = await database.CanConnectAsync(ct);
        return ready ? Ok(new { status = "ready", database = "available" })
            : StatusCode(503, new { status = "not-ready", database = "unavailable" });
    }
}
