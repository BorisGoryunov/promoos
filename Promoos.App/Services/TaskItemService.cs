using Promoos.Data;
using Promoos.Entities;
using Microsoft.EntityFrameworkCore;
using Promoos.Core;
using Promoos.App.Dto;

namespace Promoos.App.Services;

public class TaskItemService
{
    private readonly AppDbContext _appDbContext;

    public TaskItemService(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task Create(TaskItemModel model)
    {
        var entity = new TaskItem
        {
            TaskId = model.TaskId,
            CreatedAt = DateTime.UtcNow,
            Priority = model.Priority,
            Title = model.Title
        };

        await _appDbContext.AddAsync(entity);
        await _appDbContext.SaveChangesAsync();
    }

    public async Task Complete(Guid taskId)
    {
        var rowCount = await _appDbContext.Set<TaskItem>()
            .Where(x => !x.IsCompleted)
            .Where(x => x.TaskId == taskId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(y => y.CompletedAt, DateTime.UtcNow)
                .SetProperty(y => y.IsCompleted, true));

        if (rowCount == 0)
        {
            throw new ConflictException("Конфликт обновления.");
        }
    }

    public async Task Delete(Guid taskId)
    {
        await _appDbContext.Set<TaskItem>()
            .Where(x => x.TaskId == taskId)
            .ExecuteDeleteAsync();
    }
}
