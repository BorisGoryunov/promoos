using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Diagnostics;

namespace Promoos.Data;

public static class Installer
{
    public static void AddData(this IServiceCollection services, IConfiguration configuration)
    {
        const string connectionName = "Default";

        var connectionString = configuration.GetConnectionString(connectionName);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception($"Строка подключения {connectionName} не найдена.");
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var searchPaths = connectionStringBuilder.SearchPath?.Split(',');

        var mainSchema = searchPaths is { Length: > 0 }
            ? searchPaths[0]
            : default;

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, o =>
            {
                if (!string.IsNullOrEmpty(mainSchema))
                {
                    o.MigrationsHistoryTable(HistoryRepository.DefaultTableName, mainSchema);
                }
            });

#if DEBUG
            options.EnableSensitiveDataLogging();
            options.LogTo(l => Debug.WriteLine(l));
#endif
        });
    }

    public static async Task Migrate(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation($"Применение миграций.");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Применение миграций - OK.");
    }


    public static void LogDbConnectionParams(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);

        logger.LogInformation("");
        logger.LogInformation($"Параметры подключения к БД.");
        logger.LogInformation($"Хост: {connectionStringBuilder.Host}");
        logger.LogInformation($"БД: {connectionStringBuilder.Database}");
        logger.LogInformation($"Схема: {connectionStringBuilder.SearchPath}");
        logger.LogInformation($"Пользователь: {connectionStringBuilder.Username}");
        logger.LogInformation("");
    }
}

