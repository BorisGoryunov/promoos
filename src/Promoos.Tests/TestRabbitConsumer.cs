using Promoos.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;

namespace Promoos.Tests;

public sealed class TestRabbitConsumer : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queue;
    private readonly ConcurrentQueue<(string RoutingKey, string Body)> _messages = new();

    public string Queue => _queue;

    private TestRabbitConsumer(IConnection connection, IChannel channel, string queue)
    {
        _connection = connection;
        _channel = channel;
        _queue = queue;
    }

    public static async Task<TestRabbitConsumer> CreateAsync(RabbitMqConfig cfg, string exchange, string routingKey, CancellationToken ct = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = cfg.Host,
            Port = cfg.Port,
            UserName = cfg.UserName,
            Password = cfg.Password,
            VirtualHost = cfg.VirtualHost,
        };

        var connection = await factory.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // эксклюзивная очередь: имя генерит брокер, удалится при закрытии соединения
        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: exchange,
            routingKey: routingKey,
            cancellationToken: ct);

        var consumer = new TestRabbitConsumer(connection, channel, queue.QueueName);

        var asyncConsumer = new AsyncEventingBasicConsumer(channel);
        asyncConsumer.ReceivedAsync += (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.Span);
            consumer._messages.Enqueue((ea.RoutingKey, body));
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer: asyncConsumer, cancellationToken: ct);

        return consumer;
    }

    /// Ждёт одно сообщение до таймаута. Возвращает (routingKey, body) или null.
    public async Task<(string RoutingKey, string Body)?> WaitForMessageAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_messages.TryDequeue(out var msg)) return msg;
            await Task.Delay(50);
        }
        return null;
    }

    public IReadOnlyList<(string RoutingKey, string Body)> Drain()
        => [.. _messages];

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _channel.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
