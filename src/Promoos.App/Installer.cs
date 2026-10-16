using Microsoft.Extensions.DependencyInjection;
using Promoos.App.Services;

namespace Promoos.App;

public static class Installer
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<TaskItemService>();
        services.AddSingleton<TaskEventPublisher>();
    }

    public static void AppServicesInitialize(this IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        var _ = scope.ServiceProvider.GetRequiredService<TaskEventPublisher>();
    }
}
