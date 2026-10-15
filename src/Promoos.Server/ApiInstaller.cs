using Promoos.App.Dto;

namespace Promoos.Server;

public static class ApiInstaller
{
    private const int _maxTitleLength = 200;

    public static void MapApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/tasks", (int limit, int offset) =>
        {
            return Results.Ok();
        });

        endpoints.MapPost("/tasks", (TaskItemModel model) =>
        {
            if (string.IsNullOrEmpty(model.Title))
            {
                throw new InvalidOperationException("Title должна быть заполнена.");
            }

            if (model.Title.Length > _maxTitleLength)
            {
                throw new InvalidOperationException($"Title должен быть равен или меньше {_maxTitleLength}.");
            }

            return Results.Ok();
        });

        endpoints.MapPut("/tasks/{id:guid}/complete", (Guid id) =>
        {
            return Results.Ok();
        });
    }
}
