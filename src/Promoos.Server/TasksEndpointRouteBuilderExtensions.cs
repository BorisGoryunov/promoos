using Microsoft.AspNetCore.Mvc;
using Promoos.App.Dto;
using Promoos.App.Services;

namespace Promoos.Server;

public static class TasksEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTasks(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
               .MapGroup("/tasks")
               .WithTags("Tasks")      
               .RequireAuthorization();

        endpoints.MapGet("/tasks", async (int limit, int offset, [FromServices] TaskItemService taskItemservice) =>
        {
            var items = await taskItemservice.GetAll(limit, offset);
            return Results.Ok(items);
        }).Produces<List<TaskItemDto>>();

        endpoints.MapPost("/tasks", async (CreateTaskItem model, [FromServices] TaskItemService taskItemservice) =>
        {
            var taskId = await taskItemservice.Create(model);
            return Results.Created($"/tasks/{taskId}", taskId);
        }).Produces<Guid>(StatusCodes.Status201Created);

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

        return endpoints;
    }
}
