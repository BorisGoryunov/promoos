namespace Promoos.Core;

public class RabbitMqConfig
{
    public required string Host { get; init; }

    public required string UserName { get; init; }

    public required string Password { get; init; }

    public required int Port { get; init; }

    public required string VirtualHost { get; init; }
}
