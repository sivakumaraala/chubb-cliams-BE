using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Persistence;

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> PendingAsync(DateTimeOffset now, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class OutboxStore(ClaimsDbContext db) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxMessage>> PendingAsync(DateTimeOffset now, CancellationToken ct) =>
        await db.OutboxMessages.Where(x => x.PublishedAtUtc == null && x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.OccurredAtUtc).Take(20).ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
