using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class GatewayRabbitMqListener : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName;
    private readonly JsonSerializerOptions _serializerOptions;

    public GatewayRabbitMqListener(GatewayRabbitMqSettings settings, string routingKey)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(settings.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

        _queueName = _channel.QueueDeclare(queue: string.Empty, durable: false, exclusive: true, autoDelete: true).QueueName;
        _channel.QueueBind(_queueName, settings.ExchangeName, routingKey);
        _channel.BasicQos(0, 1, false);

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public Task<TEvent> WaitForEventAsync<TEvent>(TimeSpan timeout, Func<TEvent, bool>? predicate = null)
    {
        var completion = new TaskCompletionSource<TEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (_, args) =>
        {
            var payload = Encoding.UTF8.GetString(args.Body.ToArray());
            var eventData = JsonSerializer.Deserialize<TEvent>(payload, _serializerOptions);
            if (eventData != null && (predicate == null || predicate(eventData)))
            {
                completion.TrySetResult(eventData);
            }

            _channel.BasicAck(args.DeliveryTag, multiple: false);
            await Task.CompletedTask;
        };

        _channel.BasicConsume(_queueName, autoAck: false, consumer);
        return completion.Task.WaitAsync(timeout);
    }

    public ValueTask DisposeAsync()
    {
        _channel.Close();
        _connection.Close();
        _channel.Dispose();
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
