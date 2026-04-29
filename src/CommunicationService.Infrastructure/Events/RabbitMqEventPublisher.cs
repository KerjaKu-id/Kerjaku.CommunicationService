using System.Text;
using System.Text.Json;
using CommunicationService.Application.Abstractions.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CommunicationService.Infrastructure.Events;

public sealed class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly ConnectionFactory _factory;
    private readonly object _sync = new();
    private IConnection? _connection;

    public RabbitMqEventPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(Math.Max(1, _options.NetworkRecoveryIntervalSeconds))
        };
    }

    public async Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        if (connection == null)
        {
            _logger.LogError("RabbitMQ connection is unavailable. Event was not published.");
            return;
        }

        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

        var payload = JsonSerializer.Serialize(eventData);
        var body = Encoding.UTF8.GetBytes(payload);

        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;

        var routingKey = eventData?.GetType().Name ?? "event";
        channel.BasicPublish(_options.ExchangeName, routingKey, properties, body);
    }

    private async Task<IConnection?> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        var retryCount = Math.Max(1, _options.ConnectionRetryCount);
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.ConnectionRetryDelaySeconds));

        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var connection = _factory.CreateConnection();
                lock (_sync)
                {
                    _connection?.Dispose();
                    _connection = connection;
                }

                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ connection attempt {Attempt} of {Total} failed.", attempt, retryCount);
                if (attempt == retryCount)
                {
                    return null;
                }

                await Task.Delay(delay, cancellationToken);
            }
        }

        return null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
