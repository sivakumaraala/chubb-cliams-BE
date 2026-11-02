using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Chubb.Claims.Persistence;
using Chubb.Claims.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Chubb.Claims.Api.Tests;

// EN-03: disabling initialization allows genuine middleware/contract checks without
// pretending to have verified persistence. SQL-backed behaviour is tested separately.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string? SqlConnection { get; init; }
    public string EnvironmentName { get; init; } = Environments.Development;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClaimsDatabase"] = SqlConnection ??
                    "Server=localhost,1;Database=ChubbClaimsUnused;User Id=unused;Password=unused;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1",
                ["Database:InitializeOnStartup"] = (SqlConnection is not null).ToString(),
                ["Kafka:Enabled"] = "false",
                ["DemoIdentity:Enabled"] = (EnvironmentName == Environments.Development).ToString()
            }));
        builder.ConfigureTestServices(services =>
        {
            // WebApplicationFactory configuration callbacks can run after the application's
            // eager registration captured its connection. Replace that registration
            // explicitly so SQL tests cannot hit the application database or start
            // real consumers against their isolated database.
            services.RemoveAll<ClaimsDbContext>();
            services.RemoveAll<DbContextOptions<ClaimsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ClaimsDbContext>>();
            services.AddDbContext<ClaimsDbContext>(options => options.UseSqlServer(SqlConnection ??
                "Server=localhost,1;Database=ChubbClaimsUnused;User Id=unused;Password=unused;Encrypt=True;TrustServerCertificate=True;Connect Timeout=1"));
            foreach (var descriptor in services.Where(x => x.ServiceType == typeof(IHostedService) &&
                         x.ImplementationType?.Namespace == "Chubb.Claims.Infrastructure" &&
                         x.ImplementationType.Name is "OutboxPublisher" or "DecisionNotificationConsumer").ToArray())
                services.Remove(descriptor);
        });
    }

    public HttpClient As(string? identity)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (identity is not null)
            client.DefaultRequestHeaders.Add("X-Demo-User", identity);
        return client;
    }
}
