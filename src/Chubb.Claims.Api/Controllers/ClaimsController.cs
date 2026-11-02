using Chubb.Claims.Api.Contracts;
using Chubb.Claims.Api.Security;
using Chubb.Claims.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chubb.Claims.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/claims")]
public sealed class ClaimsController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "Claimant")]
    [ProducesResponseType<ClaimView>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClaimView>> Submit([FromBody] SubmitClaimRequest request,
        [FromServices] SubmitClaimHandler handler, CancellationToken ct)
    {
        var claim = await handler.Handle(new(request.Type, request.IncidentDate, request.Description,
            request.Market, request.Currency, request.ClaimedAmount, User.ActorId()), ct);
        return Created($"/api/claims/{claim.Id}", ClaimResponseMapper.View(claim));
    }

    [HttpGet]
    [Authorize(Policy = "Claimant")]
    [ProducesResponseType<PageResponse<ClaimView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResponse<ClaimView>>> List([FromServices] ListClaimsHandler handler,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ClaimResponseMapper.Page(await handler.Handle(new(User.ActorId(), false, null, null, page, pageSize), ct)));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClaimView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimView>> Get(Guid id, [FromServices] GetClaimHandler handler, CancellationToken ct)
    {
        var claim = await handler.Handle(new(id, User.ActorId(), User.IsInRole("Officer")), ct);
        return claim is null ? NotFound() : Ok(ClaimResponseMapper.View(claim));
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<ClaimHistoryEntry[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClaimHistoryEntry>>> History(Guid id,
        [FromServices] GetClaimHistoryHandler handler, CancellationToken ct)
    {
        var rows = await handler.Handle(new(id, User.ActorId(), User.IsInRole("Officer")), ct);
        return rows is null ? NotFound() : Ok(rows);
    }

    [HttpGet("{id:guid}/notifications")]
    [Authorize(Policy = "Claimant")]
    [ProducesResponseType<ClaimNotificationResult[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClaimNotificationResult>>> Notifications(Guid id,
        [FromServices] GetClaimNotificationsHandler handler, CancellationToken ct)
    {
        var rows = await handler.Handle(new(id, User.ActorId(), false), ct);
        return rows is null ? NotFound() : Ok(rows);
    }

    [HttpPost("{id:guid}/assignment")]
    [Authorize(Policy = "Officer")]
    [ProducesResponseType<ClaimView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimView>> Assign(Guid id, [FromBody] AssignClaimRequest request,
        [FromServices] AssignClaimHandler handler, CancellationToken ct)
    {
        var claim = await handler.Handle(new(id, request.RowVersion, User.ActorId()), ct);
        return claim is null ? NotFound() : Ok(ClaimResponseMapper.View(claim));
    }

    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = "Officer")]
    [ProducesResponseType<ClaimView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimView>> Decide(Guid id, [FromBody] DecisionRequest request,
        [FromServices] DecideClaimHandler handler, CancellationToken ct)
    {
        var claim = await handler.Handle(new(id, request.RowVersion, request.Outcome,
            request.Reason, request.ApprovedAmount, User.ActorId(), HttpContext.TraceIdentifier), ct);
        return claim is null ? NotFound() : Ok(ClaimResponseMapper.View(claim));
    }

    [HttpPost("{id:guid}/information-requests")]
    [Authorize(Policy = "Officer")]
    [ProducesResponseType<ClaimView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimView>> RequestInformation(Guid id, [FromBody] RequestInformationRequest request,
        [FromServices] RequestInformationHandler handler, CancellationToken ct)
    {
        var claim = await handler.Handle(new(id, request.RowVersion, request.Question, User.ActorId()), ct);
        return claim is null ? NotFound() : Ok(ClaimResponseMapper.View(claim));
    }

    [HttpPost("{id:guid}/information-requests/{requestId:guid}/response")]
    [Authorize(Policy = "Claimant")]
    [ProducesResponseType<ClaimView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimView>> ProvideInformation(Guid id, Guid requestId,
        [FromBody] ProvideInformationRequest request, [FromServices] ProvideInformationHandler handler,
        CancellationToken ct)
    {
        var claim = await handler.Handle(new(id, requestId, request.RowVersion, request.Response, User.ActorId()), ct);
        return claim is null ? NotFound() : Ok(ClaimResponseMapper.View(claim));
    }

    [HttpPost("{id:guid}/settlements")]
    [Authorize(Policy = "Officer")]
    [ProducesResponseType<ClaimView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimView>> RecordSettlementPayment(Guid id,
        [FromBody] RecordSettlementPaymentRequest request,
        [FromServices] RecordSettlementPaymentHandler handler, CancellationToken ct)
    {
        var claim = await handler.Handle(new(id, request.RowVersion, request.Amount, request.Currency,
            request.ExternalReference, request.PaymentDate, User.ActorId()), ct);
        return claim is null ? NotFound() : Ok(ClaimResponseMapper.View(claim));
    }
}
