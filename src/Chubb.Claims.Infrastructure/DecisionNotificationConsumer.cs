using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Chubb.Claims.Application;
using Chubb.Claims.Persistence;

namespace Chubb.Claims.Infrastructure;

internal sealed class DecisionNotificationConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    KafkaTopicInitializer topicInitializer,
    ILogger<DecisionNotificationConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await topicInitializer.WaitUntilReadyAsync(stoppingToken);
            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                GroupId = options.Value.GroupId,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                SocketTimeoutMs = 5000
            }).Build();
            consumer.Subscribe(options.Value.Topic);
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string> record;
                    try { record = consumer.Consume(stoppingToken); }
                    catch (ConsumeException exception)
                    {
                        logger.LogWarning("Kafka consume failed with {ErrorCode}; retrying in 5 seconds", exception.Error.Code);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    ClaimDecisionRecorded message;
                    try
                    {
                        message = JsonSerializer.Deserialize<ClaimDecisionRecorded>(record.Message.Value, ClaimDecisionRecorded.JsonOptions)
                            ?? throw new InvalidDataException("Empty decision event.");
                        message.Validate();
                    }
                    catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentNullException)
                    {
                        await PauseForRecoveryAsync(record, exception.GetType().Name, stoppingToken);
                        continue;
                    }

                    using var logScope = logger.BeginScope(new Dictionary<string, object>
                    {
                        ["EventId"] = message.EventId, ["ClaimId"] = message.ClaimId,
                        ["CorrelationId"] = message.CorrelationId, ["Operation"] = "ConsumeDecision"
                    });
                    var attempts = 0;
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            await using var scope = scopeFactory.CreateAsyncScope();
                            var handler = scope.ServiceProvider.GetRequiredService<DecisionNotificationHandler>();
                            var created = await handler.ProcessAsync(message, stoppingToken);
                            consumer.Commit(record);
                            logger.LogInformation("Decision event processed; notification created {Created}", created);
                            break;
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (KafkaException exception)
                        {
                            if (exception.Error.Code is ErrorCode.Local_State or ErrorCode.IllegalGeneration
                                or ErrorCode.UnknownMemberId or ErrorCode.RebalanceInProgress)
                            {
                                logger.LogWarning("Consumer assignment changed with {ErrorCode}; polling to rejoin. " +
                                    "Any replay remains protected by the processed event ID", exception.Error.Code);
                                break;
                            }
                            logger.LogWarning("Offset commit failed with {ErrorCode}; retrying safely in 5 seconds", exception.Error.Code);
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        }
                        catch (InvalidDataException exception)
                        {
                            await PauseForRecoveryAsync(record, exception.GetType().Name, stoppingToken);
                        }
                        catch (Exception exception)
                        {
                            attempts++;
                            logger.LogError(exception, "Decision processing failed on attempt {Attempt}; offset is uncommitted", attempts);
                            if (attempts >= 5)
                                await PauseForRecoveryAsync(record, exception.GetType().Name, stoppingToken);
                            await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempts * 5)), stoppingToken);
                        }
                    }
                }
            }
            finally
            {
                try { consumer.Close(); }
                catch (KafkaException exception)
                {
                    logger.LogWarning("Consumer closed without a clean group leave ({ErrorCode}); uncommitted records may replay", exception.Error.Code);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task PauseForRecoveryAsync(ConsumeResult<string, string> record, string errorType, CancellationToken cancellationToken)
    {
        logger.LogError("Decision processing paused at {Topic}/{Partition}/{Offset} after {ErrorType}. " +
            "No offset was committed. Inspect the record and repair its cause, then restart the application to replay it. " +
            "There is no automatic poison-message quarantine in this demo.",
            record.Topic, record.Partition.Value, record.Offset.Value, errorType);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
