namespace CommunicationService.Infrastructure.Events;

public sealed class RabbitMqOptions
{
    public string HostName { get; init; } = "rabbitmq";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string ExchangeName { get; init; } = "communication.events";
    public int ConnectionRetryCount { get; init; } = 5;
    public int ConnectionRetryDelaySeconds { get; init; } = 5;
    public int NetworkRecoveryIntervalSeconds { get; init; } = 10;
}
