using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chubb.Claims.Infrastructure;

internal sealed class KafkaTopicInitializer(IOptions<KafkaOptions> options, ILogger<KafkaTopicInitializer> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            while (!_ready && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var admin = new AdminClientBuilder(new AdminClientConfig
                    {
                        BootstrapServers = options.Value.BootstrapServers,
                        SocketTimeoutMs = 5000
                    }).Build();
                    await admin.CreateTopicsAsync([
                        new TopicSpecification { Name = options.Value.Topic, NumPartitions = 1, ReplicationFactor = 1 }
                    ], new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(5) })
                        .WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                    _ready = true;
                }
                catch (CreateTopicsException exception) when (
                    exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    _ready = true;
                }
                catch (Exception exception) when (exception is KafkaException or TimeoutException)
                {
                    logger.LogWarning("Kafka topic setup unavailable ({ErrorType}); retrying in 5 seconds", exception.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        finally { _gate.Release(); }
    }
}
