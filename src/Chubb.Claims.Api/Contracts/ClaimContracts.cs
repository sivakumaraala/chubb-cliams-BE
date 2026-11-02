using System.ComponentModel.DataAnnotations;
using Chubb.Claims.Application;

namespace Chubb.Claims.Api.Contracts;

public sealed record SubmitClaimRequest(ClaimType Type, DateOnly IncidentDate,
    [param: Required, MaxLength(2000)] string Description,
    [param: Required] string Market, [param: Required] string Currency, decimal ClaimedAmount);
public sealed record AssignClaimRequest([param: Required] string RowVersion);
public sealed record DecisionRequest([param: Required] string RowVersion, ClaimStatus Outcome,
    [param: Required, MaxLength(2000)] string Reason, decimal? ApprovedAmount = null);
public sealed record RequestInformationRequest([param: Required] string RowVersion,
    [param: Required, MaxLength(2000)] string Question);
public sealed record ProvideInformationRequest([param: Required] string RowVersion,
    [param: Required, MaxLength(2000)] string Response);
public sealed record RecordSettlementPaymentRequest([param: Required] string RowVersion,
    decimal Amount, [param: Required] string Currency,
    [param: Required, MaxLength(100)] string ExternalReference, DateOnly PaymentDate);
public sealed record ClaimView(Guid Id, string Reference, string OwnerId, ClaimType Type, DateOnly IncidentDate,
    string Description, string Market, string Currency, decimal ClaimedAmount, decimal? ApprovedAmount,
    decimal ReserveAmount, string? AssignedOfficerId, ClaimStatus Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc, string RowVersion, IReadOnlyList<DecisionView> Decisions,
    IReadOnlyList<InformationRequestView> InformationRequests,
    decimal PaidAmount, IReadOnlyList<SettlementPaymentView> SettlementPayments);
public sealed record DecisionView(Guid Id, string OfficerId, ClaimStatus Outcome, decimal? ApprovedAmount,
    string Reason, DateTimeOffset OccurredAtUtc);
public sealed record InformationRequestView(Guid Id, string OfficerId, string Question,
    DateTimeOffset RequestedAtUtc, string? Response, DateTimeOffset? RespondedAtUtc);
public sealed record SettlementPaymentView(Guid Id, string OfficerId, decimal Amount, string Currency,
    string ExternalReference, DateOnly PaymentDate, DateTimeOffset RecordedAtUtc);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class ClaimResponseMapper
{
    public static ClaimView View(Claim claim) => new(claim.Id, claim.Reference, claim.OwnerId,
        claim.Type, claim.IncidentDate, claim.Description, claim.Market, claim.Currency,
        claim.ClaimedAmount, claim.ApprovedAmount, claim.ReserveAmount, claim.AssignedOfficerId,
        claim.Status, claim.CreatedAtUtc, claim.UpdatedAtUtc, Convert.ToBase64String(claim.RowVersion),
        claim.Decisions.OrderBy(x => x.OccurredAtUtc).Select(x => new DecisionView(x.Id, x.OfficerId,
            x.Outcome, x.ApprovedAmount, x.Reason, x.OccurredAtUtc)).ToArray(),
        claim.InformationRequests.OrderBy(x => x.RequestedAtUtc).Select(x =>
            new InformationRequestView(x.Id, x.OfficerId, x.Question, x.RequestedAtUtc,
                x.Response, x.RespondedAtUtc)).ToArray(),
        claim.PaidAmount, claim.SettlementPayments.OrderBy(x => x.RecordedAtUtc).Select(x =>
            new SettlementPaymentView(x.Id, x.OfficerId, x.Amount, x.Currency, x.ExternalReference,
                x.PaymentDate, x.RecordedAtUtc)).ToArray());

    public static PageResponse<ClaimView> Page(PageResult<Claim> page) =>
        new(page.Items.Select(View).ToArray(), page.Total, page.Page, page.PageSize);
}
