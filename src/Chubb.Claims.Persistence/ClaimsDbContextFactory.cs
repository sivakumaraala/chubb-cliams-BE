using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chubb.Claims.Persistence;

public sealed class ClaimsDbContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    public ClaimsDbContext CreateDbContext(string[] args)
    {
        // Creating migrations/scripts does not connect. Runtime configuration belongs to the API.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ClaimsDatabase")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ChubbClaims;Integrated Security=True;TrustServerCertificate=True";
        return new ClaimsDbContext(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(connection).Options);
    }
}
