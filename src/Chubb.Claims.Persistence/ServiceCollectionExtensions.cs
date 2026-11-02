using Chubb.Claims.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chubb.Claims.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClaimsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("ClaimsDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:ClaimsDatabase is required.");
        services.AddDbContext<ClaimsDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<ClaimRepository>();
        services.AddScoped<IClaimQueryRepository>(sp => sp.GetRequiredService<ClaimRepository>());
        services.AddScoped<IClaimCommandRepository>(sp => sp.GetRequiredService<ClaimRepository>());
        services.AddScoped<DecisionNotificationHandler>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IDatabaseHealth, DatabaseHealth>();
        return services;
    }
}
