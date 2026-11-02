using Chubb.Claims.Application;

namespace Chubb.Claims.Application;

public sealed record ListClaimsQuery(string ActorId, bool IsOfficer, ClaimStatus? Status,
    string? Assignment, int Page, int PageSize);
public sealed record GetClaimQuery(Guid ClaimId, string ActorId, bool IsOfficer);

public sealed class ListClaimsHandler(IClaimQueryRepository repository)
{
    public async Task<PageResult<Claim>> Handle(ListClaimsQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            throw new DomainValidationException("Page must be positive and pageSize between 1 and 100.");
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new DomainValidationException("Unknown claim status.");
        if (query.Assignment is not (null or "" or "mine" or "unassigned"))
            throw new DomainValidationException("Assignment filter must be mine or unassigned.");
        var skip = (long)(query.Page - 1) * query.PageSize;
        if (skip > int.MaxValue) throw new DomainValidationException("Requested page is too large.");
        var (items, total) = await repository.ListAsync(query.IsOfficer ? null : query.ActorId,
            query.Status, query.Assignment == "mine" ? query.ActorId : null,
            query.Assignment == "unassigned", (int)skip, query.PageSize, ct);
        return new(items, total, query.Page, query.PageSize);
    }
}

public sealed class GetClaimHandler(IClaimQueryRepository repository)
{
    public async Task<Claim?> Handle(GetClaimQuery query, CancellationToken ct) =>
        ClaimRules.Visible(await repository.GetAsync(query.ClaimId, ct), query.ActorId, query.IsOfficer);
}

public sealed class GetClaimHistoryHandler(GetClaimHandler getClaim, IClaimQueryRepository repository)
{
    public async Task<IReadOnlyList<ClaimHistoryEntry>?> Handle(GetClaimQuery query, CancellationToken ct) =>
        await getClaim.Handle(query, ct) is null ? null : await repository.HistoryAsync(query.ClaimId, ct);
}

public sealed class GetClaimNotificationsHandler(GetClaimHandler getClaim, IClaimQueryRepository repository)
{
    public async Task<IReadOnlyList<ClaimNotificationResult>?> Handle(GetClaimQuery query, CancellationToken ct) =>
        await getClaim.Handle(query, ct) is null ? null : await repository.NotificationsAsync(query.ClaimId, query.ActorId, ct);
}

public sealed class GetOfficerDashboardHandler(IClaimQueryRepository repository)
{
    public Task<OfficerDashboard> Handle(string officerId, CancellationToken ct) => repository.DashboardAsync(officerId, ct);
}
