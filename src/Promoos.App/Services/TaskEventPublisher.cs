using System.Text;
using System.Text.Json;
using Promoos.App.Dto;
using RabbitMQ.Client;

namespace Promoos.App.Services;

public class TaskEventPublisher : IAsyncDisposable
{
    private readonly IConnection _connection;

    private readonly IChannel _channel;

    public TaskEventPublisher()
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest",
            Port = 5672,
            VirtualHost = "/"
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(
            exchange: "task.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();
    }

    public async Task PublishTaskCompleted(TaskCompletedEvent evt)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel.BasicPublishAsync(
            exchange: "task.events",
            routingKey: "task.completed",
            mandatory: false,
            basicProperties: props,
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}
