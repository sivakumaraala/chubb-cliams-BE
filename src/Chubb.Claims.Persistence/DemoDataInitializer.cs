using Chubb.Claims.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chubb.Claims.Persistence;

public static class DemoDataInitializer
{
    public static async Task InitializeClaimsDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        await db.Database.MigrateAsync();
        if (await db.Claims.AnyAsync()) return;
        var now = DateTimeOffset.UtcNow;
        db.Claims.Add(Claim.Submit("CLM-DEMO-MOTOR", "claimant-alex", ClaimType.Motor,
            DateOnly.FromDateTime(now.AddDays(-2).UtcDateTime), "Illustrative motor incident", "M01", "SGD", 1200m, now));
        db.Claims.Add(Claim.Submit("CLM-DEMO-PROPERTY", "claimant-blair", ClaimType.Property,
            DateOnly.FromDateTime(now.AddDays(-3).UtcDateTime), "Illustrative property incident", "M02", "AUD", 2600m, now));
        await db.SaveChangesAsync();
    }
}
