using Chubb.Claims.Persistence;
using Chubb.Claims.Application;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chubb.Claims.Api.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CHUBB_TEST_SQL")))
            Skip = "Real SQL Server test not run: set CHUBB_TEST_SQL to a local test-server connection string.";
    }
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string databaseName = "ChubbClaimsApiTests_" + Guid.NewGuid().ToString("N");
    private string? connectionString;
    public ApiFactory Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("CHUBB_TEST_SQL");
        if (string.IsNullOrWhiteSpace(supplied))
            return Task.CompletedTask;

        // Never run migration/deletion against a supplied application's database.
        var builder = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName };
        connectionString = builder.ConnectionString;
        Factory = new ApiFactory { SqlConnection = connectionString };
        using var startupClient = Factory.As(null);
        return Task.CompletedTask;
    }

    public async Task<T> InDatabase<T>(Func<ClaimsDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ClaimsDbContext>());
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null)
            return;

        await Factory.DisposeAsync();
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (builder.InitialCatalog != databaseName || !databaseName.StartsWith("ChubbClaimsApiTests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the unique test database.");

        await using var db = new ClaimsDbContext(new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer(connectionString).Options);
        await db.Database.EnsureDeletedAsync();
    }
}
