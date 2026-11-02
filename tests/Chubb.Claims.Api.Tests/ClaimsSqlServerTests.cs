using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chubb.Claims.Persistence;
using Chubb.Claims.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chubb.Claims.Api.Tests;

// These tests intentionally require SQL Server: they verify rowversion and atomic
// persistence, not the different semantics of an in-memory or SQLite substitute.
[Trait("Category", "SqlServerIntegration")]
public sealed class ClaimsSqlServerTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task Offline_partial_payments_settle_once_and_release_currency_exposure()
    {
        using var claimant = fixture.Factory.As("claimant-alex");
        using var officer = fixture.Factory.As("officer-lee");
        using var otherOfficer = fixture.Factory.As("officer-sam");
        var submitted = await Submit(claimant);
        var assigned = await PostClaim(officer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });
        var approved = await PostClaim(officer, $"/api/claims/{submitted.Id}/decision", new
        {
            assigned.RowVersion, outcome = "Approved", approvedAmount = 800m, reason = "Demo approval"
        });
        var url = $"/api/claims/{submitted.Id}/settlements";
        var before = (await officer.GetFromJsonAsync<JsonObject>("/api/officer/claims/summary"))!;
        var firstBody = new { approved.RowVersion, amount = 300m, currency = "SGD",
            externalReference = "OFFLINE-1", paymentDate = "2026-01-02" };
        Assert.Equal(HttpStatusCode.Forbidden, (await claimant.PostAsJsonAsync(url, firstBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherOfficer.PostAsJsonAsync(url, firstBody)).StatusCode);
        var partial = await PostClaim(officer, url, firstBody);
        Assert.Equal("Approved", partial.Status);
        Assert.Equal(500m, partial.ReserveAmount);
        Assert.Equal(HttpStatusCode.Conflict, (await officer.PostAsJsonAsync(url, firstBody)).StatusCode);
        var duplicate = await officer.PostAsJsonAsync(url, new { partial.RowVersion, amount = 100m,
            currency = "SGD", externalReference = "OFFLINE-1", paymentDate = "2026-01-02" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var overpay = await officer.PostAsJsonAsync(url, new { partial.RowVersion, amount = 501m,
            currency = "SGD", externalReference = "OFFLINE-2", paymentDate = "2026-01-02" });
        Assert.Equal(HttpStatusCode.BadRequest, overpay.StatusCode);
        var settled = await PostClaim(officer, url, new { partial.RowVersion, amount = 500m,
            currency = "SGD", externalReference = "OFFLINE-2", paymentDate = "2026-01-02" });
        Assert.Equal("Settled", settled.Status);
        Assert.Equal(0m, settled.ReserveAmount);
        Assert.Equal(2, await fixture.InDatabase(db => db.SettlementPayments.CountAsync(x => x.ClaimId == submitted.Id)));
        Assert.Equal(5, await fixture.InDatabase(db => db.History.CountAsync(x => x.ClaimId == submitted.Id)));
        var after = (await officer.GetFromJsonAsync<JsonObject>("/api/officer/claims/summary"))!;
        var beforeSgd = ((JsonArray)before["exposure"]!).Single(x => x!["currency"]!.GetValue<string>() == "SGD")!["amount"]!.GetValue<decimal>();
        var afterSgd = ((JsonArray)after["exposure"]!).SingleOrDefault(x => x!["currency"]!.GetValue<string>() == "SGD")?["amount"]?.GetValue<decimal>() ?? 0m;
        Assert.Equal(beforeSgd - 800m, afterSgd);
    }

    [SqlServerFact]
    public async Task Officer_summary_tracks_workload_currency_exposure_and_all_time_decisions()
    {
        using var owner = fixture.Factory.As("claimant-alex");
        using var officer = fixture.Factory.As("officer-lee");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.GetAsync("/api/officer/claims/summary")).StatusCode);
        var before = (await officer.GetFromJsonAsync<JsonObject>("/api/officer/claims/summary"))!;
        Assert.Contains((JsonArray)before["decisions"]!, x => x!["officerId"]!.GetValue<string>() == "officer-sam");
        var submitted = await Submit(owner);
        var assigned = await PostClaim(officer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });
        await PostClaim(officer, $"/api/claims/{submitted.Id}/decision", new
        {
            assigned.RowVersion, outcome = "Approved", approvedAmount = 400m, reason = "Demo covered amount"
        });
        var audResponse = await owner.PostAsJsonAsync("/api/claims", new
        {
            type = "Property", incidentDate = "2026-01-01", description = "Illustrative property incident",
            market = "M02", currency = "AUD", claimedAmount = 250m
        });
        Assert.Equal(HttpStatusCode.Created, audResponse.StatusCode);
        var aud = (await audResponse.Content.ReadFromJsonAsync<ClaimSnapshot>())!;
        var audAssigned = await PostClaim(officer, $"/api/claims/{aud.Id}/assignment", new { aud.RowVersion });
        await PostClaim(officer, $"/api/claims/{aud.Id}/decision", new
        {
            audAssigned.RowVersion, outcome = "Rejected", approvedAmount = (decimal?)null, reason = "Demo rejection"
        });
        var after = (await officer.GetFromJsonAsync<JsonObject>("/api/officer/claims/summary"))!;
        Assert.Equal(before["totalClaims"]!.GetValue<int>() + 2, after["totalClaims"]!.GetValue<int>());
        Assert.Equal(before["openClaims"]!.GetValue<int>() + 1, after["openClaims"]!.GetValue<int>());
        var beforeSgd = ((JsonArray)before["exposure"]!).SingleOrDefault(x => x!["currency"]!.GetValue<string>() == "SGD")?["amount"]?.GetValue<decimal>() ?? 0m;
        var afterSgd = ((JsonArray)after["exposure"]!).Single(x => x!["currency"]!.GetValue<string>() == "SGD")!["amount"]!.GetValue<decimal>();
        Assert.Equal(beforeSgd + 400m, afterSgd);
        var beforeAud = ((JsonArray)before["exposure"]!).SingleOrDefault(x => x!["currency"]!.GetValue<string>() == "AUD")?["amount"]?.GetValue<decimal>() ?? 0m;
        var afterAud = ((JsonArray)after["exposure"]!).SingleOrDefault(x => x!["currency"]!.GetValue<string>() == "AUD")?["amount"]?.GetValue<decimal>() ?? 0m;
        Assert.Equal(beforeAud, afterAud);
        var beforeCount = ((JsonArray)before["decisions"]!).SingleOrDefault(x => x!["officerId"]!.GetValue<string>() == "officer-lee")?["approved"]?.GetValue<int>() ?? 0;
        var afterCount = ((JsonArray)after["decisions"]!).Single(x => x!["officerId"]!.GetValue<string>() == "officer-lee")!["approved"]!.GetValue<int>();
        Assert.Equal(beforeCount + 1, afterCount);
    }

    [SqlServerFact]
    public async Task Officer_question_and_owner_answer_round_trip_with_history_and_access_checks()
    {
        using var owner = fixture.Factory.As("claimant-alex");
        using var other = fixture.Factory.As("claimant-blair");
        using var officer = fixture.Factory.As("officer-lee");
        using var otherOfficer = fixture.Factory.As("officer-sam");
        var submitted = await Submit(owner);
        var assigned = await PostClaim(officer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });
        var url = $"/api/claims/{submitted.Id}/information-requests";
        Assert.Equal(HttpStatusCode.Forbidden, (await otherOfficer.PostAsJsonAsync(url,
            new { assigned.RowVersion, question = "Wrong officer" })).StatusCode);
        var asked = await officer.PostAsJsonAsync(url, new { assigned.RowVersion, question = "Please clarify the incident" });
        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);
        var askedBody = Assert.IsType<JsonObject>(await asked.Content.ReadFromJsonAsync<JsonNode>());
        Assert.Equal("AwaitingInformation", askedBody["status"]!.GetValue<string>());
        var requestId = askedBody["informationRequests"]![0]!["id"]!.GetValue<Guid>();
        var answerUrl = $"{url}/{requestId}/response";
        var answer = new { rowVersion = askedBody["rowVersion"]!.GetValue<string>(), response = "Baggage delay details" };
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(answerUrl, answer)).StatusCode);
        var answered = await owner.PostAsJsonAsync(answerUrl, answer);
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        var result = Assert.IsType<JsonObject>(await answered.Content.ReadFromJsonAsync<JsonNode>());
        Assert.Equal("UnderReview", result["status"]!.GetValue<string>());
        Assert.Equal("Baggage delay details", result["informationRequests"]![0]!["response"]!.GetValue<string>());
        Assert.Equal(4, await fixture.InDatabase(db => db.History.CountAsync(x => x.ClaimId == submitted.Id)));
    }

    [SqlServerFact]
    public async Task Travel_claim_round_trips_through_sql_and_the_officer_queue()
    {
        using var claimant = fixture.Factory.As("claimant-alex");
        using var officer = fixture.Factory.As("officer-lee");
        var response = await claimant.PostAsJsonAsync("/api/claims", new
        {
            type = "Travel", incidentDate = "2026-01-01", description = "Illustrative baggage delay",
            market = "M01", currency = "SGD", claimedAmount = 300m
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = Assert.IsType<JsonObject>(await response.Content.ReadFromJsonAsync<JsonNode>());
        Assert.Equal("Travel", submitted["type"]!.GetValue<string>());
        var id = submitted["id"]!.GetValue<Guid>();

        var storedType = await fixture.InDatabase(async db =>
            (await db.Claims.SingleAsync(x => x.Id == id)).Type);
        Assert.Equal(ClaimType.Travel, storedType);

        var queue = await officer.GetFromJsonAsync<JsonObject>("/api/officer/claims");
        var items = Assert.IsType<JsonArray>(queue!["items"]);
        Assert.Contains(items, item => item!["id"]!.GetValue<Guid>() == id &&
            item["type"]!.GetValue<string>() == "Travel");
    }

    [SqlServerFact]
    public async Task Submit_pickup_approve_records_history_and_one_outbox_event()
    {
        using var claimant = fixture.Factory.As("claimant-alex");
        using var officer = fixture.Factory.As("officer-lee");
        var submitted = await Submit(claimant);

        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal("claimant-alex", submitted.OwnerId);
        Assert.False(string.IsNullOrEmpty(submitted.RowVersion));

        var assigned = await PostClaim(officer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });
        Assert.Equal("UnderReview", assigned.Status);
        Assert.NotEqual(submitted.RowVersion, assigned.RowVersion);

        var decided = await PostClaim(officer, $"/api/claims/{submitted.Id}/decision", new
        {
            assigned.RowVersion, outcome = "Approved", approvedAmount = 750m, reason = "Demo review completed"
        });
        Assert.Equal("Approved", decided.Status);
        Assert.Equal(1000m, decided.ClaimedAmount);
        Assert.Equal(750m, decided.ApprovedAmount);
        Assert.Equal(750m, decided.ReserveAmount);

        var stored = await fixture.InDatabase(async db => new
        {
            History = await db.History.CountAsync(x => x.ClaimId == submitted.Id),
            Decisions = await db.Decisions.CountAsync(x => x.ClaimId == submitted.Id),
            Outbox = await db.OutboxMessages.CountAsync(x => x.ClaimId == submitted.Id)
        });
        Assert.Equal(3, stored.History);
        Assert.Equal(1, stored.Decisions);
        Assert.Equal(1, stored.Outbox);
    }

    [SqlServerFact]
    public async Task Another_claimant_cannot_read_claim_detail_history_or_notifications()
    {
        using var owner = fixture.Factory.As("claimant-alex");
        using var other = fixture.Factory.As("claimant-blair");
        var submitted = await Submit(owner);

        foreach (var suffix in new[] { "", "/history", "/notifications" })
        {
            var response = await other.GetAsync($"/api/claims/{submitted.Id}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var list = await other.GetFromJsonAsync<JsonObject>("/api/claims");
        var items = Assert.IsType<JsonArray>(list!["items"]);
        Assert.DoesNotContain(items, x => x!["id"]!.GetValue<Guid>() == submitted.Id);
    }

    [SqlServerFact]
    public async Task Competing_pickups_have_one_winner_without_extra_history()
    {
        using var owner = fixture.Factory.As("claimant-alex");
        using var first = fixture.Factory.As("officer-lee");
        using var second = fixture.Factory.As("officer-sam");
        var submitted = await Submit(owner);

        var attempts = await Task.WhenAll(
            first.PostAsJsonAsync($"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion }),
            second.PostAsJsonAsync($"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion }));
        Assert.Equal(1, attempts.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, attempts.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(2, await fixture.InDatabase(db => db.History.CountAsync(x => x.ClaimId == submitted.Id)));
    }

    [SqlServerFact]
    public async Task Stale_decision_is_rolled_back_without_decision_history_or_outbox()
    {
        using var claimant = fixture.Factory.As("claimant-alex");
        using var officer = fixture.Factory.As("officer-lee");
        var submitted = await Submit(claimant);
        await PostClaim(officer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });

        var response = await officer.PostAsJsonAsync($"/api/claims/{submitted.Id}/decision", new
        {
            submitted.RowVersion, outcome = "Approved", approvedAmount = 750m, reason = "Stale update"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = await fixture.InDatabase(async db => new
        {
            Status = (await db.Claims.SingleAsync(x => x.Id == submitted.Id)).Status.ToString(),
            History = await db.History.CountAsync(x => x.ClaimId == submitted.Id),
            Decisions = await db.Decisions.CountAsync(x => x.ClaimId == submitted.Id),
            Outbox = await db.OutboxMessages.CountAsync(x => x.ClaimId == submitted.Id)
        });
        Assert.Equal("UnderReview", stored.Status);
        Assert.Equal(2, stored.History);
        Assert.Equal(0, stored.Decisions);
        Assert.Equal(0, stored.Outbox);
    }

    [SqlServerFact]
    public async Task SQL_rowversion_conflict_rolls_back_added_decision_history_and_event()
    {
        using var claimant = fixture.Factory.As("claimant-alex");
        using var officer = fixture.Factory.As("officer-lee");
        var submitted = await Submit(claimant);
        await PostClaim(officer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });

        await using var firstScope = fixture.Factory.Services.CreateAsyncScope();
        await using var secondScope = fixture.Factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var staleClaim = await secondDb.Claims.SingleAsync(x => x.Id == submitted.Id);

        // Inject the exact interleaving an early HTTP token check cannot prevent:
        // another database writer changes this row after it was loaded but before save.
        await firstDb.Claims.Where(x => x.Id == submitted.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow));

        var now = DateTimeOffset.UtcNow;
        var decision = staleClaim.Approve("officer-lee", 750m, "Attempt after concurrent change", now);
        secondDb.OutboxMessages.Add(OutboxMessage.From(new ClaimDecisionRecorded(decision.Id, 1,
            staleClaim.Id, staleClaim.Reference, staleClaim.OwnerId, "Approved", now, "sql-concurrency-test")));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());

        var stored = await fixture.InDatabase(async db => new
        {
            History = await db.History.CountAsync(x => x.ClaimId == submitted.Id),
            Decisions = await db.Decisions.CountAsync(x => x.ClaimId == submitted.Id),
            Outbox = await db.OutboxMessages.CountAsync(x => x.ClaimId == submitted.Id)
        });
        Assert.Equal(2, stored.History);
        Assert.Equal(0, stored.Decisions);
        Assert.Equal(0, stored.Outbox);
    }

    [SqlServerFact]
    public async Task Wrong_officer_and_invalid_amount_cannot_create_a_decision()
    {
        using var claimant = fixture.Factory.As("claimant-alex");
        using var assignedOfficer = fixture.Factory.As("officer-lee");
        using var otherOfficer = fixture.Factory.As("officer-sam");
        var submitted = await Submit(claimant);
        var assigned = await PostClaim(assignedOfficer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });

        var wrongActor = await otherOfficer.PostAsJsonAsync($"/api/claims/{submitted.Id}/decision", new
        {
            assigned.RowVersion, outcome = "Approved", approvedAmount = 750m, reason = "Not my assignment"
        });
        Assert.Equal(HttpStatusCode.Forbidden, wrongActor.StatusCode);
        var invalidAmount = await assignedOfficer.PostAsJsonAsync($"/api/claims/{submitted.Id}/decision", new
        {
            assigned.RowVersion, outcome = "Approved", approvedAmount = 1001m, reason = "Too high"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAmount.StatusCode);
        Assert.Equal(0, await fixture.InDatabase(db => db.OutboxMessages.CountAsync(x => x.ClaimId == submitted.Id)));
        Assert.Equal(2, await fixture.InDatabase(db => db.History.CountAsync(x => x.ClaimId == submitted.Id)));
    }

    [SqlServerFact]
    public async Task Repeated_event_processing_creates_one_owner_visible_notification()
    {
        using var claimant = fixture.Factory.As("claimant-alex");
        using var officer = fixture.Factory.As("officer-lee");
        var submitted = await Submit(claimant);
        var assigned = await PostClaim(officer, $"/api/claims/{submitted.Id}/assignment", new { submitted.RowVersion });
        await PostClaim(officer, $"/api/claims/{submitted.Id}/decision", new
        {
            assigned.RowVersion, outcome = "Rejected", approvedAmount = (decimal?)null, reason = "Demo rejection"
        });
        var message = await fixture.InDatabase(async db => JsonSerializer.Deserialize<ClaimDecisionRecorded>(
            (await db.OutboxMessages.SingleAsync(x => x.ClaimId == submitted.Id)).Payload,
            ClaimDecisionRecorded.JsonOptions)!);

        Assert.True(await fixture.InDatabase(db => new DecisionNotificationHandler(db).ProcessAsync(message)));
        Assert.False(await fixture.InDatabase(db => new DecisionNotificationHandler(db).ProcessAsync(message)));
        Assert.Equal(1, await fixture.InDatabase(db => db.Notifications.CountAsync(x => x.EventId == message.EventId)));
        Assert.Equal(1, await fixture.InDatabase(db => db.ProcessedEvents.CountAsync(x => x.EventId == message.EventId)));

        var response = await claimant.GetAsync($"/api/claims/{submitted.Id}/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(Assert.IsType<JsonArray>(await response.Content.ReadFromJsonAsync<JsonNode>()));
    }

    private static async Task<ClaimSnapshot> Submit(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/claims", new
        {
            type = "Motor", incidentDate = "2026-01-01", description = "Demo incident for API verification",
            market = "M01", currency = "SGD", claimedAmount = 1000m
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClaimSnapshot>())!;
    }

    private static async Task<ClaimSnapshot> PostClaim(HttpClient client, string route, object body)
    {
        var response = await client.PostAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClaimSnapshot>())!;
    }

    private sealed record ClaimSnapshot(Guid Id, string RowVersion, string Status, string OwnerId,
        decimal ClaimedAmount, decimal? ApprovedAmount, decimal ReserveAmount);
}
