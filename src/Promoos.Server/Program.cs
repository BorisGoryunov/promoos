using NLog;
using NLog.Web;
using Promoos.App;
using Promoos.Data;
using Promoos.Server;
using Scalar.AspNetCore;

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
    builder.Services.AddData(builder.Configuration);
    builder.Services.AddAppServices();

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.Services.AppServicesInitialize();

    app.Services.LogDbConnectionParams();

    await app.Services.Migrate();

    app.MapApi();
    app.Run();
}
catch (Exception exception)
{
    logger.Error(exception);
}
finally
{
    LogManager.Shutdown();
}
