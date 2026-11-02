using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Persistence;

public interface IDatabaseHealth
{
    Task<bool> CanConnectAsync(CancellationToken ct);
}

public sealed class DatabaseHealth(ClaimsDbContext db) : IDatabaseHealth
{
    public async Task<bool> CanConnectAsync(CancellationToken ct)
    {
        try { return await db.Database.CanConnectAsync(ct); }
        catch (Exception) { return false; }
    }
}
