using System.Text.Json;
using Chubb.Claims.Application;

namespace Chubb.Claims.Persistence;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public static OutboxMessage From(ClaimDecisionRecorded message)
    {
        message.Validate();
        return new OutboxMessage
        {
            Id = message.EventId,
            ClaimId = message.ClaimId,
            Payload = JsonSerializer.Serialize(message, ClaimDecisionRecorded.JsonOptions),
            CorrelationId = message.CorrelationId,
            OccurredAtUtc = message.OccurredAtUtc.ToUniversalTime(),
            NextAttemptAtUtc = message.OccurredAtUtc.ToUniversalTime()
        };
    }

    public void RecordAttempt() => Attempts++;
    public void MarkPublished(DateTimeOffset now) => PublishedAtUtc = now.ToUniversalTime();
    public void ScheduleRetry(DateTimeOffset now) =>
        NextAttemptAtUtc = now.AddSeconds(Math.Min(30, Math.Pow(2, Math.Min(Attempts, 5)))).ToUniversalTime();
}

public sealed record ClaimNotification(
    Guid Id,
    Guid EventId,
    Guid ClaimId,
    string OwnerId,
    string Message,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessedEvent(Guid EventId, DateTimeOffset ProcessedAtUtc);
