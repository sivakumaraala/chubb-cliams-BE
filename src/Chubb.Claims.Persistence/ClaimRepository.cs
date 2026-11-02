using Chubb.Claims.Application;
using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Persistence;

public sealed class ClaimRepository(ClaimsDbContext db) : IClaimQueryRepository, IClaimCommandRepository
{
    public Task<Claim?> GetAsync(Guid id, CancellationToken ct) =>
        db.Claims.AsNoTracking().Include(x => x.Decisions).Include(x => x.InformationRequests)
            .Include(x => x.SettlementPayments).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Claim?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        db.Claims.Include(x => x.Decisions).Include(x => x.InformationRequests)
            .Include(x => x.SettlementPayments).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<Claim> Items, int Total)> ListAsync(string? ownerId, ClaimStatus? status,
        string? assignedOfficerId, bool unassignedOnly, int skip, int pageSize, CancellationToken ct)
    {
        var query = db.Claims.AsNoTracking();
        if (ownerId is not null) query = query.Where(x => x.OwnerId == ownerId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (assignedOfficerId is not null) query = query.Where(x => x.AssignedOfficerId == assignedOfficerId);
        if (unassignedOnly) query = query.Where(x => x.AssignedOfficerId == null);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(skip).Take(pageSize).Include(x => x.Decisions).Include(x => x.InformationRequests)
            .Include(x => x.SettlementPayments).AsSplitQuery().ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<ClaimHistoryEntry>> HistoryAsync(Guid claimId, CancellationToken ct) =>
        await db.History.AsNoTracking().Where(x => x.ClaimId == claimId)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<ClaimNotificationResult>> NotificationsAsync(Guid claimId, string ownerId,
        CancellationToken ct) => await db.Notifications.AsNoTracking()
        .Where(x => x.ClaimId == claimId && x.OwnerId == ownerId)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new ClaimNotificationResult(x.Id, x.EventId, x.ClaimId, x.OwnerId, x.Message, x.CreatedAtUtc))
        .ToListAsync(ct);

    public void Add(Claim claim) => db.Claims.Add(claim);
    public async Task<OfficerDashboard> DashboardAsync(string officerId, CancellationToken ct)
    {
        var statuses = await db.Claims.AsNoTracking().GroupBy(x => x.Status)
            .Select(group => new StatusCount(group.Key, group.Count())).ToListAsync(ct);
        var exposureRows = await db.Claims.AsNoTracking().Where(x => x.ReserveAmount > 0)
            .GroupBy(x => x.Currency)
            .Select(group => new { Currency = group.Key, Amount = group.Sum(x => x.ReserveAmount), ClaimCount = group.Count() })
            .ToListAsync(ct);
        var exposure = exposureRows.OrderBy(x => x.Currency)
            .Select(x => new CurrencyExposure(x.Currency, x.Amount, x.ClaimCount)).ToArray();
        var decisionRows = await db.Decisions.AsNoTracking().GroupBy(x => x.OfficerId)
            .Select(group => new { OfficerId = group.Key,
                Approved = group.Count(x => x.Outcome == ClaimStatus.Approved),
                Rejected = group.Count(x => x.Outcome == ClaimStatus.Rejected) })
            .ToListAsync(ct);
        var decisions = decisionRows.OrderBy(x => x.OfficerId)
            .Select(x => new OfficerDecisionCount(x.OfficerId, x.Approved, x.Rejected)).ToArray();
        var total = statuses.Sum(x => x.Count);
        var open = statuses.Where(x => x.Status is ClaimStatus.Submitted or ClaimStatus.UnderReview
            or ClaimStatus.AwaitingInformation or ClaimStatus.Approved).Sum(x => x.Count);
        var unassigned = await db.Claims.AsNoTracking().CountAsync(x => x.AssignedOfficerId == null, ct);
        var mine = await db.Claims.AsNoTracking().CountAsync(x => x.AssignedOfficerId == officerId &&
            x.Status != ClaimStatus.Rejected && x.Status != ClaimStatus.Settled, ct);
        return new(total, open, unassigned, mine, statuses, exposure, decisions);
    }
    public void QueueDecision(ClaimDecisionRecorded decision) => db.OutboxMessages.Add(OutboxMessage.From(decision));
    public void SetExpectedVersion(Claim claim, byte[] rowVersion) =>
        db.Entry(claim).Property(x => x.RowVersion).OriginalValue = rowVersion;
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ClaimConflictException("This claim changed. Refresh it before trying again.");
        }
    }
}
