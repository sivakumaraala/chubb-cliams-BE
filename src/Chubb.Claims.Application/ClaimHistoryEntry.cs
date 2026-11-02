namespace Chubb.Claims.Application;

public sealed class ClaimHistoryEntry
{
    private ClaimHistoryEntry() { }

    internal ClaimHistoryEntry(
        Guid claimId,
        string actorId,
        ClaimHistoryAction action,
        ClaimStatus? fromStatus,
        ClaimStatus toStatus,
        decimal? reserveBefore,
        decimal reserveAfter,
        DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        ClaimId = claimId;
        ActorId = actorId;
        Action = action;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ReserveBefore = reserveBefore;
        ReserveAfter = reserveAfter;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public ClaimHistoryAction Action { get; private set; }
    public ClaimStatus? FromStatus { get; private set; }
    public ClaimStatus ToStatus { get; private set; }
    public decimal? ReserveBefore { get; private set; }
    public decimal ReserveAfter { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
