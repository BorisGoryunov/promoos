using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Promoos.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
               .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .Build();

        var connectionString = configuration.GetConnectionString("Default");

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var searchPaths = connectionStringBuilder.SearchPath?.Split(',');

        var mainSchema = searchPaths is { Length: > 0 }
            ? searchPaths[0]
            : default;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseNpgsql(connectionString, x =>
        {
            if (!string.IsNullOrEmpty(mainSchema))
            {
                x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, mainSchema);
            }
        });

        return new AppDbContext(optionsBuilder.Options);
    }
}
