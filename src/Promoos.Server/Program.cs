using NLog;
using NLog.Web;
using Promoos.App;
using Promoos.Core;
using Promoos.Data;
using Promoos.Server;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Info("Start...");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Add services to the container.
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Configuration.AddEnvironmentVariables();

    var rabbitMqConfig = builder.Configuration
        .GetRequiredSection(nameof(RabbitMqConfig))
        .Get<RabbitMqConfig>();

    if (rabbitMqConfig is null)
    {
        throw new NullReferenceException(nameof(rabbitMqConfig));
    }

    builder.Services.AddSingleton(rabbitMqConfig);

    builder.Services.AddData(builder.Configuration);
    builder.Services.AddAppServices();
    builder.Services.ConfigureHttpJsonOptions(o =>
    {
        o.SerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
    });

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseExceptionHandler();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.Services.LogDbConnectionParams();

    if (builder.Configuration.GetValue("Database:AutoMigrate", true))
    {
        await app.Services.Migrate();
    }

    app.MapTasks();
    app.Run();
}
catch (Exception exception)
{
    logger.Error(exception);
    throw;
}
finally
{
    LogManager.Shutdown();
}

public partial class Program { } // нужно для WebApplicationFactory
