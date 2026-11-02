namespace Chubb.Claims.Application;

public sealed class ClaimSettlementPayment
{
    private ClaimSettlementPayment() { }
    internal ClaimSettlementPayment(Guid claimId, string officerId, decimal amount, string currency,
        string externalReference, DateOnly paymentDate, DateTimeOffset recordedAtUtc)
    {
        Id = Guid.NewGuid(); ClaimId = claimId; OfficerId = officerId; Amount = amount;
        Currency = currency; ExternalReference = externalReference; PaymentDate = paymentDate;
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
    }
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public string OfficerId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string ExternalReference { get; private set; } = string.Empty;
    public DateOnly PaymentDate { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
