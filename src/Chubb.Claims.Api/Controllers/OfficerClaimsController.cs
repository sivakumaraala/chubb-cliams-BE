using Chubb.Claims.Api.Contracts;
using Chubb.Claims.Api.Security;
using Chubb.Claims.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chubb.Claims.Api.Controllers;

[ApiController]
[Authorize(Policy = "Officer")]
[Route("api/officer/claims")]
public sealed class OfficerClaimsController : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<OfficerDashboard>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OfficerDashboard>> Summary([FromServices] GetOfficerDashboardHandler handler,
        CancellationToken ct)
    {
        var summary = await handler.Handle(User.ActorId(), ct);
        var officers = DemoUsers.All.Where(x => x.Role == "Officer")
            .Select(user => summary.Decisions.FirstOrDefault(x => x.OfficerId == user.Id)
                ?? new OfficerDecisionCount(user.Id, 0, 0)).ToArray();
        return Ok(summary with { Decisions = officers });
    }

    [HttpGet]
    [ProducesResponseType<PageResponse<ClaimView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResponse<ClaimView>>> List([FromServices] ListClaimsHandler handler,
        [FromQuery] ClaimStatus? status = null, [FromQuery] string? assignment = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ClaimResponseMapper.Page(await handler.Handle(
            new(User.ActorId(), true, status, assignment, page, pageSize), ct)));
}
