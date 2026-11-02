using Chubb.Claims.Application;
using Microsoft.Extensions.Logging;

namespace Chubb.Claims.Application;

public sealed record SubmitClaimCommand(ClaimType Type, DateOnly IncidentDate, string Description,
    string Market, string Currency, decimal ClaimedAmount, string ActorId);
public sealed record AssignClaimCommand(Guid ClaimId, string RowVersion, string ActorId);
public sealed record DecideClaimCommand(Guid ClaimId, string RowVersion, ClaimStatus Outcome,
    string Reason, decimal? ApprovedAmount, string ActorId, string CorrelationId);
public sealed record RequestInformationCommand(Guid ClaimId, string RowVersion, string Question, string ActorId);
public sealed record ProvideInformationCommand(Guid ClaimId, Guid RequestId, string RowVersion, string Response, string ActorId);
public sealed record RecordSettlementPaymentCommand(Guid ClaimId, string RowVersion, decimal Amount,
    string Currency, string ExternalReference, DateOnly PaymentDate, string ActorId);

public sealed class RecordSettlementPaymentHandler(IClaimCommandRepository repository, TimeProvider clock)
{
    public async Task<Claim?> Handle(RecordSettlementPaymentCommand command, CancellationToken ct)
    {
        var claim = await repository.GetForUpdateAsync(command.ClaimId, ct);
        if (claim is null) return null;
        ClaimRules.CheckVersion(claim, command.RowVersion, repository);
        claim.RecordSettlementPayment(command.ActorId, command.Amount, command.Currency,
            command.ExternalReference, command.PaymentDate, clock.GetUtcNow());
        await repository.SaveAsync(ct);
        return claim;
    }
}

public sealed class RequestInformationHandler(IClaimCommandRepository repository, TimeProvider clock)
{
    public async Task<Claim?> Handle(RequestInformationCommand command, CancellationToken ct)
    {
        var claim = await repository.GetForUpdateAsync(command.ClaimId, ct);
        if (claim is null) return null;
        ClaimRules.CheckVersion(claim, command.RowVersion, repository);
        claim.RequestInformation(command.ActorId, command.Question, clock.GetUtcNow());
        await repository.SaveAsync(ct);
        return claim;
    }
}

public sealed class ProvideInformationHandler(IClaimCommandRepository repository, TimeProvider clock)
{
    public async Task<Claim?> Handle(ProvideInformationCommand command, CancellationToken ct)
    {
        var claim = await repository.GetForUpdateAsync(command.ClaimId, ct);
        if (claim is null || claim.OwnerId != command.ActorId) return null;
        ClaimRules.CheckVersion(claim, command.RowVersion, repository);
        claim.ProvideInformation(command.ActorId, command.RequestId, command.Response, clock.GetUtcNow());
        await repository.SaveAsync(ct);
        return claim;
    }
}

public sealed class SubmitClaimHandler(IClaimCommandRepository repository, DemoMarketCatalog catalog,
    TimeProvider clock, ILogger<SubmitClaimHandler> logger)
{
    public async Task<Claim> Handle(SubmitClaimCommand command, CancellationToken ct)
    {
        ClaimRules.ValidateText(command.Description, "Description", 2000);
        ClaimRules.ValidateMoney(command.ClaimedAmount, "Claimed amount");
        var market = catalog.Markets.SingleOrDefault(x => x.Code == command.Market);
        if (market is null || !market.Currencies.Contains(command.Currency, StringComparer.Ordinal))
            throw new DomainValidationException("Select a configured demo market and currency.");
        var id = Guid.NewGuid();
        var claim = Claim.Submit($"CLM-{id:N}", command.ActorId, command.Type, command.IncidentDate,
            command.Description, command.Market, command.Currency, command.ClaimedAmount, clock.GetUtcNow(), id);
        repository.Add(claim);
        await repository.SaveAsync(ct);
        logger.LogInformation("Claim {ClaimId} submitted in {Market}", claim.Id, claim.Market);
        return claim;
    }
}

public sealed class AssignClaimHandler(IClaimCommandRepository repository, TimeProvider clock,
    ILogger<AssignClaimHandler> logger)
{
    public async Task<Claim?> Handle(AssignClaimCommand command, CancellationToken ct)
    {
        var claim = await repository.GetForUpdateAsync(command.ClaimId, ct);
        if (claim is null) return null;
        ClaimRules.CheckVersion(claim, command.RowVersion, repository);
        claim.AssignTo(command.ActorId, clock.GetUtcNow());
        await repository.SaveAsync(ct);
        logger.LogInformation("Claim {ClaimId} assigned to {OfficerId}", claim.Id, command.ActorId);
        return claim;
    }
}

public sealed class DecideClaimHandler(IClaimCommandRepository repository, TimeProvider clock,
    ILogger<DecideClaimHandler> logger)
{
    public async Task<Claim?> Handle(DecideClaimCommand command, CancellationToken ct)
    {
        var claim = await repository.GetForUpdateAsync(command.ClaimId, ct);
        if (claim is null) return null;
        ClaimRules.CheckVersion(claim, command.RowVersion, repository);
        ClaimRules.ValidateText(command.Reason, "Decision reason", 2000);
        var now = clock.GetUtcNow();
        ClaimDecision decision;
        if (command.Outcome == ClaimStatus.Approved && command.ApprovedAmount.HasValue)
        {
            ClaimRules.ValidateMoney(command.ApprovedAmount.Value, "Approved amount");
            decision = claim.Approve(command.ActorId, command.ApprovedAmount.Value, command.Reason, now);
        }
        else if (command.Outcome == ClaimStatus.Rejected && command.ApprovedAmount is null)
            decision = claim.Reject(command.ActorId, command.Reason, now);
        else throw new DomainValidationException("Choose Approved with an amount, or Rejected without an approved amount.");

        repository.QueueDecision(new ClaimDecisionRecorded(decision.Id, 1, claim.Id, claim.Reference,
            claim.OwnerId, decision.Outcome.ToString(), now, command.CorrelationId));
        await repository.SaveAsync(ct); // claim, history, decision and outbox share one EF transaction
        logger.LogInformation("Claim {ClaimId} transitioned to {Status}; event {EventId} queued",
            claim.Id, claim.Status, decision.Id);
        return claim;
    }
}
