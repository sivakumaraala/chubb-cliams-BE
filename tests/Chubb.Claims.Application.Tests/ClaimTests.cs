using Chubb.Claims.Application;
using Xunit;

namespace Chubb.Claims.Application.Tests;

public sealed class ClaimTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Submission_preserves_money_owner_and_an_initial_audit_entry()
    {
        var claim = NewClaim(1234.56m);

        Assert.Equal("claimant-a", claim.OwnerId);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal("EUR", claim.Currency);
        Assert.Equal(1234.56m, claim.ClaimedAmount);
        Assert.Equal(1234.56m, claim.ReserveAmount);
        Assert.Null(claim.ApprovedAmount);
        Assert.Null(claim.AssignedOfficerId);
        var entry = Assert.Single(claim.History);
        Assert.Equal(claim.Id, entry.ClaimId);
        Assert.Equal("claimant-a", entry.ActorId);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ClaimStatus.Submitted, entry.ToStatus);
        Assert.Equal(1234.56m, entry.ReserveAfter);
        Assert.Equal(Now, entry.OccurredAtUtc);
        Assert.Empty(claim.Decisions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Submission_rejects_nonpositive_claimed_amounts(int amount)
    {
        Assert.Throws<DomainValidationException>(() => NewClaim(amount));
    }

    [Fact]
    public void Submission_rejects_a_future_incident_date()
    {
        Assert.Throws<DomainValidationException>(() => Claim.Submit(
            "CLM-TEST", "claimant-a", ClaimType.Motor, new DateOnly(2026, 11, 3),
            "Incident", "Demo market", "EUR", 100m, Now));
    }

    [Fact]
    public void Travel_submission_uses_the_shared_claim_lifecycle()
    {
        var claim = Claim.Submit("CLM-TRAVEL", "claimant-a", ClaimType.Travel,
            new DateOnly(2026, 11, 1), "Delayed baggage", "M01", "SGD", 300m, Now);

        Assert.Equal(ClaimType.Travel, claim.Type);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(300m, claim.ReserveAmount);
        Assert.Single(claim.History);

        claim.AssignTo("officer-a", Now.AddMinutes(1));
        claim.Approve("officer-a", 200m, "Eligible demo expense", Now.AddMinutes(2));
        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(200m, claim.ReserveAmount);
        Assert.Equal(3, claim.History.Count);
    }

    [Fact]
    public void Information_exchange_requires_assigned_officer_and_owner_and_returns_to_review()
    {
        var claim = NewClaim();
        claim.AssignTo("officer-a", Now.AddMinutes(1));
        Assert.Throws<ClaimAccessDeniedException>(() => claim.RequestInformation("officer-b", "Question", Now.AddMinutes(2)));
        var request = claim.RequestInformation("officer-a", "Please clarify", Now.AddMinutes(2));
        Assert.Equal(ClaimStatus.AwaitingInformation, claim.Status);
        Assert.Throws<ClaimConflictException>(() => claim.RequestInformation("officer-a", "Another", Now.AddMinutes(3)));
        Assert.Throws<ClaimAccessDeniedException>(() => claim.ProvideInformation("claimant-b", request.Id, "Answer", Now.AddMinutes(3)));
        claim.ProvideInformation("claimant-a", request.Id, "  Answer  ", Now.AddMinutes(3));
        Assert.Equal("Answer", request.Response);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(4, claim.History.Count);
        Assert.Throws<ClaimConflictException>(() => claim.ProvideInformation("claimant-a", request.Id, "Again", Now.AddMinutes(4)));
    }

    [Fact]
    public void Multiple_offline_payments_reduce_reserve_and_settle_only_when_fully_paid()
    {
        var claim = NewClaim(1000m);
        claim.AssignTo("officer-a", Now.AddMinutes(1));
        claim.Approve("officer-a", 800m, "Approved", Now.AddMinutes(2));
        var date = new DateOnly(2026, 11, 1);
        Assert.Throws<ClaimAccessDeniedException>(() => claim.RecordSettlementPayment("officer-b", 300m, "EUR", "P-1", date, Now));
        claim.RecordSettlementPayment("officer-a", 300m, "EUR", "P-1", date, Now.AddMinutes(3));
        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(500m, claim.ReserveAmount);
        Assert.Equal(300m, claim.PaidAmount);
        Assert.Throws<ClaimConflictException>(() => claim.RecordSettlementPayment("officer-a", 100m, "EUR", "p-1", date, Now.AddMinutes(4)));
        Assert.Throws<DomainValidationException>(() => claim.RecordSettlementPayment("officer-a", 501m, "EUR", "P-2", date, Now.AddMinutes(4)));
        Assert.Throws<DomainValidationException>(() => claim.RecordSettlementPayment("officer-a", 100m, "USD", "P-2", date, Now.AddMinutes(4)));
        claim.RecordSettlementPayment("officer-a", 500m, "EUR", "P-2", date, Now.AddMinutes(4));
        Assert.Equal(ClaimStatus.Settled, claim.Status);
        Assert.Equal(0m, claim.ReserveAmount);
        Assert.Equal(800m, claim.PaidAmount);
        Assert.Equal(2, claim.SettlementPayments.Count);
        Assert.Equal(5, claim.History.Count);
        Assert.Throws<ClaimConflictException>(() => claim.RecordSettlementPayment("officer-a", 1m, "EUR", "P-3", date, Now.AddMinutes(5)));
    }

    [Fact]
    public void Submission_rejects_missing_currency_and_unknown_claim_type()
    {
        Assert.Throws<DomainValidationException>(() => Claim.Submit(
            "CLM-TEST", "claimant-a", ClaimType.Motor, new DateOnly(2026, 11, 1),
            "Incident", "Demo market", " ", 100m, Now));
        Assert.Throws<DomainValidationException>(() => Claim.Submit(
            "CLM-TEST", "claimant-a", (ClaimType)999, new DateOnly(2026, 11, 1),
            "Incident", "Demo market", "EUR", 100m, Now));
    }

    [Fact]
    public void Ownership_guard_rejects_another_claimant()
    {
        var claim = NewClaim();

        claim.EnsureOwnedBy("claimant-a");
        Assert.Throws<ClaimAccessDeniedException>(() => claim.EnsureOwnedBy("claimant-b"));
    }

    [Fact]
    public void Pickup_assigns_and_transitions_together_with_history()
    {
        var claim = NewClaim();

        claim.AssignTo("officer-a", Now.AddMinutes(1));

        Assert.Equal("officer-a", claim.AssignedOfficerId);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(1000m, claim.ReserveAmount);
        Assert.Equal(Now.AddMinutes(1), claim.UpdatedAtUtc);
        var entry = claim.History.Last();
        Assert.Equal(ClaimStatus.Submitted, entry.FromStatus);
        Assert.Equal(ClaimStatus.UnderReview, entry.ToStatus);
        Assert.Equal("officer-a", entry.ActorId);
    }

    [Fact]
    public void A_second_pickup_cannot_overwrite_assignment_or_append_history()
    {
        var claim = NewClaim();
        claim.AssignTo("officer-a", Now.AddMinutes(1));

        Assert.Throws<ClaimConflictException>(() => claim.AssignTo("officer-b", Now.AddMinutes(2)));

        Assert.Equal("officer-a", claim.AssignedOfficerId);
        Assert.Equal(2, claim.History.Count);
    }

    [Fact]
    public void Approval_keeps_claimed_amount_and_changes_reserve_to_the_approved_amount()
    {
        var claim = AssignedClaim(1234.56m);

        var decision = claim.Approve("officer-a", 900.12m, "Assessment accepted", Now.AddMinutes(2));

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(1234.56m, claim.ClaimedAmount);
        Assert.Equal(900.12m, claim.ApprovedAmount);
        Assert.Equal(900.12m, claim.ReserveAmount);
        Assert.Equal(claim.Id, decision.ClaimId);
        Assert.Equal("officer-a", decision.OfficerId);
        Assert.Equal(ClaimStatus.Approved, decision.Outcome);
        Assert.Same(decision, Assert.Single(claim.Decisions));
        Assert.Equal(3, claim.History.Count);
        Assert.Equal(1234.56m, claim.History.Last().ReserveBefore);
        Assert.Equal(900.12m, claim.History.Last().ReserveAfter);
    }

    [Fact]
    public void The_full_claimed_amount_can_be_approved()
    {
        var claim = AssignedClaim();

        claim.Approve("officer-a", claim.ClaimedAmount, "Accepted", Now.AddMinutes(2));

        Assert.Equal(claim.ClaimedAmount, claim.ApprovedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Invalid_approval_amount_leaves_the_aggregate_unchanged(int amount)
    {
        var claim = AssignedClaim();

        Assert.Throws<DomainValidationException>(() => claim.Approve("officer-a", amount, "Reason", Now.AddMinutes(2)));

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Null(claim.ApprovedAmount);
        Assert.Equal(1000m, claim.ReserveAmount);
        Assert.Empty(claim.Decisions);
        Assert.Equal(2, claim.History.Count);
    }

    [Fact]
    public void Decisions_require_the_assigned_officer()
    {
        var claim = AssignedClaim();

        Assert.Throws<ClaimAccessDeniedException>(() => claim.Approve("officer-b", 500m, "Reason", Now.AddMinutes(2)));
        Assert.Throws<ClaimAccessDeniedException>(() => claim.Reject("officer-b", "Reason", Now.AddMinutes(2)));

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Empty(claim.Decisions);
        Assert.Equal(2, claim.History.Count);
    }

    [Fact]
    public void A_submitted_claim_cannot_be_decided_before_pickup()
    {
        var claim = NewClaim();

        Assert.Throws<ClaimConflictException>(() => claim.Approve("officer-a", 500m, "Reason", Now.AddMinutes(1)));
        Assert.Throws<ClaimConflictException>(() => claim.Reject("officer-a", "Reason", Now.AddMinutes(1)));
        Assert.Single(claim.History);
    }

    [Fact]
    public void Decisions_require_a_reason_without_partial_state_changes()
    {
        var claim = AssignedClaim();

        Assert.Throws<DomainValidationException>(() => claim.Approve("officer-a", 500m, " ", Now.AddMinutes(2)));
        Assert.Throws<DomainValidationException>(() => claim.Reject("officer-a", " ", Now.AddMinutes(2)));
        Assert.Empty(claim.Decisions);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(2, claim.History.Count);
    }

    [Fact]
    public void Rejection_removes_exposure_and_cannot_be_decided_again()
    {
        var claim = AssignedClaim();

        var decision = claim.Reject("officer-a", "Assessment rejected", Now.AddMinutes(2));

        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Null(claim.ApprovedAmount);
        Assert.Null(decision.ApprovedAmount);
        Assert.Equal(0m, claim.ReserveAmount);
        Assert.Throws<ClaimConflictException>(() => claim.Approve("officer-a", 500m, "New reason", Now.AddMinutes(3)));
        Assert.Single(claim.Decisions);
        Assert.Equal(3, claim.History.Count);
    }

    [Fact]
    public void Approval_is_not_settlement_and_cannot_be_overwritten()
    {
        var claim = AssignedClaim();
        claim.Approve("officer-a", 500m, "Accepted", Now.AddMinutes(2));

        Assert.Throws<ClaimConflictException>(() => claim.Reject("officer-a", "Changed mind", Now.AddMinutes(3)));

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(500m, claim.ReserveAmount);
        Assert.Single(claim.Decisions);
        Assert.Equal(3, claim.History.Count);
    }

    private static Claim NewClaim(decimal amount = 1000m) => Claim.Submit(
        "CLM-TEST", "claimant-a", ClaimType.Motor, new DateOnly(2026, 11, 1),
        "Incident description", "Demo market", " eur ", amount, Now);

    private static Claim AssignedClaim(decimal amount = 1000m)
    {
        var claim = NewClaim(amount);
        claim.AssignTo("officer-a", Now.AddMinutes(1));
        return claim;
    }
}
