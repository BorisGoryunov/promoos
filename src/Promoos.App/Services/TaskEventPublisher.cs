using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promoos.App.Dto;
using Promoos.Core;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Promoos.App.Services;

public class TaskEventPublisher : IAsyncDisposable, IHostedService
{
    private IConnection? _connection;

    private IChannel? _channel;
    
    private readonly IHostApplicationLifetime _lifetime;

    private readonly RabbitMqConfig _config;

    private readonly ILogger<TaskEventPublisher> _logger;

    private readonly CancellationTokenSource _shutdownCts = new();

    private readonly List<Task> _pendingPublishes = [];

    private readonly object _lock = new();

    private readonly object _gate = new();

    private bool _started;

    private bool _disposed;

    private Task? _disposeTask;

    private CancellationTokenRegistration _stoppingRegistration;

    public TaskEventPublisher(IHostApplicationLifetime lifetime,
        RabbitMqConfig rabbitMqConfig,
        ILogger<TaskEventPublisher> logger)
    {
        _lifetime = lifetime;
        _config = rabbitMqConfig;
        _logger = logger;
    }

    public async Task PublishTaskCompleted(TaskCompletedEvent evt)
    {
        if (_shutdownCts.IsCancellationRequested)
        {
            throw new InvalidOperationException("Publisher is shutting down");
        }

        if (_channel is null)
        {
            throw new InvalidOperationException("Publisher not started.");
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var publishTask = _channel.BasicPublishAsync(
            exchange: "task.events",
            routingKey: "task.completed",
            mandatory: false,
            basicProperties: props,
            body: body).AsTask();

        lock (_lock)
        {
            _pendingPublishes.Add(publishTask);
        }

        try
        {
            await publishTask;
        }
        finally
        {
            lock (_lock)
            {
                _pendingPublishes.Remove(publishTask);
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TaskEventPublisher));
            }

            if (_started)
            {
                return;
            }

            _started = true;
        }

        _stoppingRegistration = _lifetime.ApplicationStopping.Register(OnStopping);

        var factory = new ConnectionFactory
        {
            HostName = _config.Host,
            UserName = _config.UserName,
            Password = _config.Password,
            Port = _config.Port,
            VirtualHost = _config.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: "task.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        _logger.LogInformation("TaskEventPublisher connected to RabbitMQ");
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();

    private async Task DisposeCoreAsync()
    {
        _shutdownCts.Cancel();

        // Снимаем регистрацию, чтобы OnStopping не дёрнул disposed CTS
        _stoppingRegistration.Dispose();

        // Ждём незавершённые публикации (с таймаутом)
        Task[] pending;
        lock (_lock) pending = [.. _pendingPublishes];

        if (pending.Length > 0)
        {
            var all = Task.WhenAll(pending);
            var timeout = Task.Delay(TimeSpan.FromSeconds(4));
            await Task.WhenAny(all, timeout);
        }

        // Закрываем канал и соединение — ровно один раз
        var channel = Interlocked.Exchange(ref _channel, null);
        var connection = Interlocked.Exchange(ref _connection, null);

        try
        {
            if (channel is not null)
            {
                await channel.CloseAsync();
                await channel.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing RabbitMQ channel");
        }

        try
        {
            if (connection is not null)
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing RabbitMQ connection");
        }

        _shutdownCts.Dispose();

        lock (_gate)
        {
            _disposed = true;
        }
    }

    private void OnStopping() => _shutdownCts.Cancel();
}
