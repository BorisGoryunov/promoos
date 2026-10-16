using Microsoft.AspNetCore.Mvc;
using Promoos.App.Dto;
using Promoos.App.Services;

namespace Promoos.Server;

public static class ApiInstaller
{
    public static void MapApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/tasks", async (int limit, int offset, [FromServices] TaskItemService taskItemservice) =>
        {
            var items = await taskItemservice.GetAll(limit, offset);
            return Results.Ok(items);
        });

        endpoints.MapPost("/tasks", async (CreateTaskItem model, [FromServices] TaskItemService taskItemservice) =>
        {
            await taskItemservice.Create(model);
            return Results.Ok();
        });

        endpoints.MapPut("/tasks/{id:guid}/complete", async (Guid id, [FromServices] TaskItemService taskItemservice) =>
        {
            await taskItemservice.Complete(id);
            return Results.Ok();
        });

        endpoints.MapDelete("/tasks/{id:guid}", async (Guid id, [FromServices] TaskItemService taskItemservice) =>
        {
            await taskItemservice.Delete(id);
            return Results.Ok();
        });
    }
}
