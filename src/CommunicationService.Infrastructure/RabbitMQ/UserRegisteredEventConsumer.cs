// Communication.Infrastructure/RabbitMQ/UserRegisteredEventConsumer.cs
// Place in: Kerjaku.CommunicationService/src/CommunicationService.Infrastructure/RabbitMQ/

using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunicationService.Application.IntegrationEventHandlers;
using Kerjaku.Contracts.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CommunicationService.Infrastructure.RabbitMQ;

/// <summary>
/// Background service that consumes UserRegisteredEvent from RabbitMQ.
///
/// Responsibilities:
/// 1. Listen to RabbitMQ queue: "user-registered.communication"
/// 2. Deserialize UserRegisteredIntegrationEvent
/// 3. Invoke UserRegisteredEventHandler
/// 4. Handle errors with DLQ and backoff
/// 5. Auto-reconnect on connection failures
///
/// Resilience:
/// - Dead Letter Exchange for failed messages
/// - Exponential backoff before retry
/// - Manual acknowledgment (only ack after handler succeeds)
/// - Auto-reconnect with exponential backoff
///
/// Config (from appsettings.json):
/// {
///   "RabbitMq": {
///     "HostName": "rabbitmq",
///     "Port": 5672,
///     "UserName": "guest",
///     "Password": "guest",
///     "VirtualHost": "/",
///     "QueueName": "user-registered.communication",
///     "ExchangeName": "user.events",
///     "RoutingKey": "user.registered"
///   }
/// }
/// </summary>
public class UserRegisteredEventConsumer : BackgroundService
{
    private readonly ILogger<UserRegisteredEventConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _options;

    private IConnection? _connection;
    private IChannel? _channel;

    public UserRegisteredEventConsumer(
        ILogger<UserRegisteredEventConsumer> logger,
        IServiceProvider serviceProvider,
        RabbitMqOptions options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options;
    }

    /// <summary>
    /// Called when service starts. Establishes RabbitMQ connection and starts listening.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UserRegisteredEventConsumer starting...");

        var retryCount = 0;
        const int maxRetries = 5;

        while (!stoppingToken.IsCancellationRequested && retryCount < maxRetries)
        {
            try
            {
                await ConnectAsync(stoppingToken);
                await ListenForEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer canceled");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                var backoffMs = (int)Math.Pow(2, retryCount) * 1000;

                _logger.LogError(ex,
                    "RabbitMQ connection failed (attempt {Attempt}/{MaxRetries}). " +
                    "Retrying in {BackoffMs}ms",
                    retryCount,
                    maxRetries,
                    backoffMs);

                await Task.Delay(backoffMs, stoppingToken);
            }
        }

        Dispose();
        _logger.LogInformation("UserRegisteredEventConsumer stopped");
    }

    /// <summary>
    /// Establish RabbitMQ connection and setup queues/exchanges.
    /// </summary>
    private async Task ConnectAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory()
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        _logger.LogInformation(
            "Connected to RabbitMQ: {HostName}:{Port}",
            _options.HostName,
            _options.Port);

        // Setup Dead Letter Exchange (DLX) for failed messages
        await SetupDeadLetterExchangeAsync();

        // Setup main exchange
        await _channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true);

        // Setup queue with DLX binding
        var queueArgs = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", $"{_options.ExchangeName}.dlx" },
            { "x-dead-letter-routing-key", "user.registered.dlq" },
            { "x-message-ttl", 86400000 }, // 24 hours
        };

        await _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs);

        // Bind queue to exchange
        await _channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey);

        // Set QoS: only process 1 message at a time
        await _channel.BasicQosAsync(0, 1, false);

        _logger.LogInformation(
            "RabbitMQ setup complete. Exchange: {Exchange}, Queue: {Queue}, RoutingKey: {RoutingKey}",
            _options.ExchangeName,
            _options.QueueName,
            _options.RoutingKey);
    }

    /// <summary>
    /// Setup Dead Letter Exchange for failed messages.
    /// </summary>
    private async Task SetupDeadLetterExchangeAsync()
    {
        const string dlxName = "user.events.dlx";
        const string dlqName = "user-registered.communication.dlq";

        await _channel!.ExchangeDeclareAsync(
            exchange: dlxName,
            type: ExchangeType.Direct,
            durable: true);

        await _channel.QueueDeclareAsync(
            queue: dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await _channel.QueueBindAsync(
            queue: dlqName,
            exchange: dlxName,
            routingKey: "user.registered.dlq");

        _logger.LogInformation(
            "Dead Letter Exchange setup: {DLX} → {DLQ}",
            dlxName,
            dlqName);
    }

    /// <summary>
    /// Listen for messages on the queue. Auto-ack only after handler succeeds.
    /// </summary>
    private async Task ListenForEventsAsync(CancellationToken ct)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.Received += async (model, ea) =>
        {
            string? correlationId = null;
            try
            {
                // Extract correlation ID for tracking
                correlationId = ea.BasicProperties?.CorrelationId;
                _logger.LogInformation(
                    "Received message from {Queue} [CorrelationId: {CorrelationId}]",
                    _options.QueueName,
                    correlationId ?? "N/A");

                // Deserialize event
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonSerializer.Deserialize<UserRegisteredIntegrationEvent>(json);

                if (@event == null)
                {
                    throw new InvalidOperationException("Failed to deserialize UserRegisteredIntegrationEvent");
                }

                _logger.LogInformation(
                    "Processing UserRegisteredEvent: UserId={UserId}, Email={Email}",
                    @event.UserId,
                    @event.Email);

                // Use DI scope to resolve handler and process event
                using (var scope = _serviceProvider.CreateScope())
                {
                    var handler = scope.ServiceProvider
                        .GetRequiredService<IIntegrationEventHandler<UserRegisteredIntegrationEvent>>();

                    await handler.HandleAsync(@event, ct);
                }

                // Auto-ack only after successful processing
                await _channel.BasicAckAsync(ea.DeliveryTag, false);

                _logger.LogInformation(
                    "Successfully processed UserRegisteredEvent [CorrelationId: {CorrelationId}]",
                    correlationId ?? "N/A");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Processing cancelled [CorrelationId: {CorrelationId}]",
                    correlationId ?? "N/A");

                // Requeue message
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing UserRegisteredEvent [CorrelationId: {CorrelationId}]. " +
                    "Message will be sent to DLQ",
                    correlationId ?? "N/A");

                // NACK without requeue → sends to DLQ
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false, // Manual ack only after processing
            consumer: consumer);

        _logger.LogInformation("Listening for UserRegisteredEvents...");

        // Keep listening until cancellation
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);
        }
    }

    /// <summary>
    /// Cleanup connection when service stops.
    /// </summary>
    public override void Dispose()
    {
        try
        {
            _channel?.CloseAsync().GetAwaiter().GetResult();
            _channel?.Dispose();
            _connection?.CloseAsync().GetAwaiter().GetResult();
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing RabbitMQ connection");
        }

        base.Dispose();
    }
}

/// <summary>
/// Configuration for RabbitMQ connection and queues.
/// Populated from appsettings.json
/// </summary>
public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "user.events";
    public string QueueName { get; set; } = "user-registered.communication";
    public string RoutingKey { get; set; } = "user.registered";
}
