namespace Chubb.Claims.Infrastructure;

public sealed class KafkaOptions
{
    public bool Enabled { get; set; } = true;
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "claims.decisions.v1";
    public string GroupId { get; set; } = "claims.notifications.v1";
}
