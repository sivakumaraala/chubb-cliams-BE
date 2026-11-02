namespace Chubb.Claims.Application;

public sealed class ClaimDecision
{
    private ClaimDecision() { }

    internal ClaimDecision(
        Guid claimId,
        string officerId,
        ClaimStatus outcome,
        decimal? approvedAmount,
        string reason,
        DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        ClaimId = claimId;
        OfficerId = officerId;
        Outcome = outcome;
        ApprovedAmount = approvedAmount;
        Reason = reason;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public string OfficerId { get; private set; } = string.Empty;
    public ClaimStatus Outcome { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
