using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Chubb.Claims.Persistence;

namespace Chubb.Claims.Infrastructure;

internal sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    KafkaTopicInitializer topicInitializer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await topicInitializer.WaitUntilReadyAsync(stoppingToken);
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                EnableIdempotence = true,
                Acks = Acks.All,
                MessageTimeoutMs = 5000,
                SocketTimeoutMs = 5000
            }).Build();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishBatchAsync(producer, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Outbox batch failed; pending records will be retried");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task PublishBatchAsync(IProducer<string, string> producer, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var now = DateTimeOffset.UtcNow;
        var messages = await store.PendingAsync(now, cancellationToken);

        foreach (var message in messages)
        {
            using var logScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["EventId"] = message.Id, ["ClaimId"] = message.ClaimId,
                ["CorrelationId"] = message.CorrelationId, ["Operation"] = "PublishDecision"
            });
            message.RecordAttempt();
            try
            {
                await producer.ProduceAsync(options.Value.Topic, new Message<string, string>
                {
                    Key = message.ClaimId.ToString(),
                    Value = message.Payload,
                    Headers = new Headers { { "correlation-id", Encoding.UTF8.GetBytes(message.CorrelationId) } }
                }, cancellationToken);
                message.MarkPublished(DateTimeOffset.UtcNow);
                logger.LogInformation("Decision event published on attempt {Attempt}", message.Attempts);
            }
            catch (ProduceException<string, string> exception)
            {
                message.ScheduleRetry(DateTimeOffset.UtcNow);
                logger.LogWarning("Decision publication failed with {ErrorCode}; attempt {Attempt}, next retry {NextAttemptAtUtc}",
                    exception.Error.Code, message.Attempts, message.NextAttemptAtUtc);
            }
            await store.SaveAsync(cancellationToken);
        }
    }
}
