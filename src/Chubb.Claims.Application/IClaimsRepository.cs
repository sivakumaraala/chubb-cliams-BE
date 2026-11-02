using Chubb.Claims.Application;

namespace Chubb.Claims.Application;

// Claim-specific repository operations; EF and IQueryable stay in Persistence.
public interface IClaimQueryRepository
{
    Task<Claim?> GetAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Claim> Items, int Total)> ListAsync(string? ownerId, ClaimStatus? status,
        string? assignedOfficerId, bool unassignedOnly, int skip, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<ClaimHistoryEntry>> HistoryAsync(Guid claimId, CancellationToken ct);
    Task<IReadOnlyList<ClaimNotificationResult>> NotificationsAsync(Guid claimId, string ownerId, CancellationToken ct);
    Task<OfficerDashboard> DashboardAsync(string officerId, CancellationToken ct);
}

public interface IClaimCommandRepository
{
    Task<Claim?> GetForUpdateAsync(Guid id, CancellationToken ct);
    void Add(Claim claim);
    void QueueDecision(ClaimDecisionRecorded decision);
    void SetExpectedVersion(Claim claim, byte[] rowVersion);
    Task SaveAsync(CancellationToken ct);
}

public sealed record ClaimNotificationResult(Guid Id, Guid EventId, Guid ClaimId, string OwnerId,
    string Message, DateTimeOffset CreatedAtUtc);

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record StatusCount(ClaimStatus Status, int Count);
public sealed record CurrencyExposure(string Currency, decimal Amount, int ClaimCount);
public sealed record OfficerDecisionCount(string OfficerId, int Approved, int Rejected);
public sealed record OfficerDashboard(int TotalClaims, int OpenClaims, int UnassignedClaims,
    int AssignedToMe, IReadOnlyList<StatusCount> Statuses, IReadOnlyList<CurrencyExposure> Exposure,
    IReadOnlyList<OfficerDecisionCount> Decisions);
