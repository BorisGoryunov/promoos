using Microsoft.Extensions.DependencyInjection;
using Promoos.App.Services;

namespace Promoos.App;

public static class Installer
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<TaskItemService>();
        services.AddSingleton<TaskEventPublisher>();
        services.AddHostedService(sp => sp.GetRequiredService<TaskEventPublisher>());
    }
}
