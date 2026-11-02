namespace Chubb.Claims.Application;

// US-02/04/05; adopted A-02, A-04, A-06 and A-07 rules.
// Persistence and messaging remain outside the aggregate.
public sealed class Claim
{
    private readonly List<ClaimHistoryEntry> _history = [];
    private readonly List<ClaimDecision> _decisions = [];
    private readonly List<ClaimInformationRequest> _informationRequests = [];
    private readonly List<ClaimSettlementPayment> _settlementPayments = [];

    private Claim() { }

    public Guid Id { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public string OwnerId { get; private set; } = string.Empty;
    public ClaimType Type { get; private set; }
    public DateOnly IncidentDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Market { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public decimal ClaimedAmount { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public decimal ReserveAmount { get; private set; }
    public string? AssignedOfficerId { get; private set; }
    public ClaimStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    // SQL Server generates this value; the API supplies expected versions to EF.
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<ClaimHistoryEntry> History => _history.AsReadOnly();
    public IReadOnlyCollection<ClaimDecision> Decisions => _decisions.AsReadOnly();
    public IReadOnlyCollection<ClaimInformationRequest> InformationRequests => _informationRequests.AsReadOnly();
    public IReadOnlyCollection<ClaimSettlementPayment> SettlementPayments => _settlementPayments.AsReadOnly();
    public decimal PaidAmount => _settlementPayments.Sum(x => x.Amount);

    public static Claim Submit(
        string reference,
        string ownerId,
        ClaimType type,
        DateOnly incidentDate,
        string description,
        string market,
        string currency,
        decimal claimedAmount,
        DateTimeOffset now,
        Guid? id = null)
    {
        if (!Enum.IsDefined(type))
            throw new DomainValidationException("Claim type must be Motor, Property, or Travel.");
        if (claimedAmount <= 0)
            throw new DomainValidationException("Claimed amount must be greater than zero.");
        if (incidentDate == default || incidentDate > DateOnly.FromDateTime(now.UtcDateTime))
            throw new DomainValidationException("Incident date is required and cannot be in the future.");
        if (id == Guid.Empty)
            throw new DomainValidationException("Claim ID must not be empty.");

        var claim = new Claim
        {
            Id = id ?? Guid.NewGuid(),
            Reference = Required(reference, "Claim reference"),
            OwnerId = Required(ownerId, "Owner"),
            Type = type,
            IncidentDate = incidentDate,
            Description = Required(description, "Incident description"),
            Market = Required(market, "Market"),
            Currency = Required(currency, "Currency").ToUpperInvariant(),
            ClaimedAmount = claimedAmount,
            ReserveAmount = claimedAmount,
            Status = ClaimStatus.Submitted,
            CreatedAtUtc = now.ToUniversalTime(),
            UpdatedAtUtc = now.ToUniversalTime()
        };

        claim._history.Add(new ClaimHistoryEntry(
            claim.Id, claim.OwnerId, ClaimHistoryAction.Submitted,
            null, claim.Status, null, claim.ReserveAmount, now));

        return claim;
    }

    public void EnsureOwnedBy(string claimantId)
    {
        if (!string.Equals(OwnerId, claimantId, StringComparison.Ordinal))
            throw new ClaimAccessDeniedException("The claim belongs to another claimant.");
    }

    public void AssignTo(string officerId, DateTimeOffset now)
    {
        var actor = Required(officerId, "Officer");
        if (Status != ClaimStatus.Submitted || AssignedOfficerId is not null)
            throw new ClaimConflictException("Only an unassigned submitted claim can be picked up.");

        AssignedOfficerId = actor;
        Transition(ClaimStatus.UnderReview, ClaimHistoryAction.Assigned, actor, ReserveAmount, now);
    }

    public ClaimInformationRequest RequestInformation(string officerId, string question, DateTimeOffset now)
    {
        if (Status != ClaimStatus.UnderReview)
            throw new ClaimConflictException("Information can only be requested while a claim is under review.");
        if (AssignedOfficerId != officerId)
            throw new ClaimAccessDeniedException("Only the assigned officer can request information.");
        if (_informationRequests.Any(x => x.Response is null))
            throw new ClaimConflictException("An information request is already open.");
        ClaimRules.ValidateText(question, "Question", 2000);
        var request = new ClaimInformationRequest(Id, officerId, question.Trim(), now);
        _informationRequests.Add(request);
        Transition(ClaimStatus.AwaitingInformation, ClaimHistoryAction.InformationRequested, officerId, ReserveAmount, now);
        return request;
    }

    public void ProvideInformation(string claimantId, Guid requestId, string response, DateTimeOffset now)
    {
        EnsureOwnedBy(claimantId);
        if (Status != ClaimStatus.AwaitingInformation)
            throw new ClaimConflictException("This claim is not awaiting information.");
        var request = _informationRequests.SingleOrDefault(x => x.Id == requestId && x.Response is null)
            ?? throw new ClaimConflictException("The information request is no longer open.");
        ClaimRules.ValidateText(response, "Response", 2000);
        request.Answer(response.Trim(), now);
        Transition(ClaimStatus.UnderReview, ClaimHistoryAction.InformationProvided, claimantId, ReserveAmount, now);
    }

    public ClaimDecision Approve(string officerId, decimal approvedAmount, string reason, DateTimeOffset now)
    {
        EnsureCanDecide(officerId);
        if (approvedAmount <= 0 || approvedAmount > ClaimedAmount)
            throw new DomainValidationException("Approved amount must be positive and no greater than the claimed amount.");
        var decisionReason = Required(reason, "Decision reason");

        var decision = new ClaimDecision(Id, officerId, ClaimStatus.Approved, approvedAmount, decisionReason, now);
        ApprovedAmount = approvedAmount;
        _decisions.Add(decision);
        Transition(ClaimStatus.Approved, ClaimHistoryAction.Approved, officerId, approvedAmount, now);
        return decision;
    }

    public ClaimDecision Reject(string officerId, string reason, DateTimeOffset now)
    {
        EnsureCanDecide(officerId);
        var decisionReason = Required(reason, "Decision reason");

        var decision = new ClaimDecision(Id, officerId, ClaimStatus.Rejected, null, decisionReason, now);
        ApprovedAmount = null;
        _decisions.Add(decision);
        Transition(ClaimStatus.Rejected, ClaimHistoryAction.Rejected, officerId, 0m, now);
        return decision;
    }

    public ClaimSettlementPayment RecordSettlementPayment(string officerId, decimal amount, string currency,
        string externalReference, DateOnly paymentDate, DateTimeOffset now)
    {
        if (Status != ClaimStatus.Approved || ApprovedAmount is null)
            throw new ClaimConflictException("Only an approved claim with an outstanding balance can record settlement.");
        if (AssignedOfficerId != officerId)
            throw new ClaimAccessDeniedException("Only the assigned officer can record settlement.");
        ClaimRules.ValidateMoney(amount, "Payment amount");
        if (currency != Currency)
            throw new DomainValidationException("Payment currency must match the claim currency.");
        ClaimRules.ValidateText(externalReference, "External reference", 100);
        var reference = externalReference.Trim();
        if (_settlementPayments.Any(x => string.Equals(x.ExternalReference, reference, StringComparison.OrdinalIgnoreCase)))
            throw new ClaimConflictException("This external payment reference has already been recorded for the claim.");
        if (paymentDate == default || paymentDate > DateOnly.FromDateTime(now.UtcDateTime))
            throw new DomainValidationException("Payment date is required and cannot be in the future.");
        var remaining = ApprovedAmount.Value - PaidAmount;
        if (amount > remaining)
            throw new DomainValidationException("Payment amount cannot exceed the outstanding approved balance.");
        var payment = new ClaimSettlementPayment(Id, officerId, amount, currency, reference, paymentDate, now);
        _settlementPayments.Add(payment);
        var reserveAfter = remaining - amount;
        Transition(reserveAfter == 0 ? ClaimStatus.Settled : ClaimStatus.Approved,
            reserveAfter == 0 ? ClaimHistoryAction.Settled : ClaimHistoryAction.PaymentRecorded,
            officerId, reserveAfter, now);
        return payment;
    }

    private void EnsureCanDecide(string officerId)
    {
        if (Status != ClaimStatus.UnderReview)
            throw new ClaimConflictException("A decision can only be recorded for a claim under review.");
        if (!string.Equals(AssignedOfficerId, officerId, StringComparison.Ordinal))
            throw new ClaimAccessDeniedException("Only the assigned officer can record a decision.");
    }

    private void Transition(ClaimStatus next, ClaimHistoryAction action, string actor, decimal reserveAfter, DateTimeOffset now)
    {
        _history.Add(new ClaimHistoryEntry(Id, actor, action, Status, next, ReserveAmount, reserveAfter, now));
        Status = next;
        ReserveAmount = reserveAfter;
        UpdatedAtUtc = now.ToUniversalTime();
    }

    private static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException($"{field} is required.");
        return value.Trim();
    }
}
