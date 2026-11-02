using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Chubb.Claims.Application;

namespace Chubb.Claims.Persistence;

// US-06: both records are saved by EF in one transaction; uniqueness also protects races.
public sealed class DecisionNotificationHandler(ClaimsDbContext db)
{
    public async Task<bool> ProcessAsync(ClaimDecisionRecorded message, CancellationToken cancellationToken = default)
    {
        message.Validate();
        if (await db.ProcessedEvents.AnyAsync(x => x.EventId == message.EventId, cancellationToken))
            return false;
        if (!await db.Claims.AnyAsync(x => x.Id == message.ClaimId && x.OwnerId == message.OwnerId, cancellationToken))
            throw new InvalidDataException("Decision event does not match an existing claim owner.");

        var now = DateTimeOffset.UtcNow;
        db.Notifications.Add(new ClaimNotification(Guid.NewGuid(), message.EventId, message.ClaimId,
            message.OwnerId, $"A {message.Outcome.ToLowerInvariant()} decision was recorded for claim {message.ClaimReference}.", now));
        db.ProcessedEvents.Add(new ProcessedEvent(message.EventId, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            if (await db.ProcessedEvents.AnyAsync(x => x.EventId == message.EventId, cancellationToken))
                return false;
            throw;
        }
    }
}
