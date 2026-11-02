using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chubb.Claims.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClaimsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = !bool.TryParse(configuration["Kafka:Enabled"], out var configuredEnabled) || configuredEnabled;
        services.Configure<KafkaOptions>(options =>
        {
            options.Enabled = enabled;
            options.BootstrapServers = configuration["Kafka:BootstrapServers"] ?? options.BootstrapServers;
            options.Topic = configuration["Kafka:Topic"] ?? options.Topic;
            options.GroupId = configuration["Kafka:GroupId"] ?? options.GroupId;
        });

        if (enabled)
        {
            services.AddSingleton<KafkaTopicInitializer>();
            services.AddHostedService<OutboxPublisher>();
            services.AddHostedService<DecisionNotificationConsumer>();
        }

        return services;
    }
}
