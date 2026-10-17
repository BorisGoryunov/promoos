using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Promoos.Core;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Promoos.Tests;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("promoos")
        .WithUsername("promoos")
        .WithPassword("promoos")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-management-alpine")
           .WithUsername("guest")
           .WithPassword("guest")
           .Build();

    private Respawner _respawner = null!;

    public HttpClient Client { get; private set; } = default!;

    public RabbitMqConfig RabbitMq { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_db.StartAsync(), _rabbit.StartAsync());

        RabbitMq = new RabbitMqConfig
        {
            Host = _rabbit.Hostname,
            Port = _rabbit.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
        };

        Client = WithWebHostBuilder(b => b
               .UseSetting("ConnectionStrings:Default", _db.GetConnectionString())
               .UseSetting("Database:AutoMigrate", "true")
               .UseSetting("RabbitMqConfig:Host", _rabbit.Hostname)
               .UseSetting("RabbitMqConfig:Port", _rabbit.GetMappedPublicPort(5672).ToString())
               .UseSetting("RabbitMqConfig:UserName", "guest")
               .UseSetting("RabbitMqConfig:Password", "guest")
               .UseSetting("RabbitMqConfig:VirtualHost", "/")
           ).CreateClient();

        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    /// Чистит данные между тестами, миграции не трогает
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    public override async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _db.DisposeAsync();
        await _rabbit.DisposeAsync();
        await base.DisposeAsync();
    }
}
