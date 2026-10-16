using Promoos.Data;
using Promoos.Entities;
using Microsoft.EntityFrameworkCore;
using Promoos.App.Dto;
using Microsoft.Extensions.Logging;
using Promoos.Core;

namespace Promoos.App.Services;

public class TaskItemService
{
    private const int _maxTitleLength = 200;

    private readonly AppDbContext _appDbContext;

    private readonly TaskEventPublisher _taskEventPublisher;
    private readonly ILogger<TaskItemService> _logger;

    public TaskItemService(AppDbContext appDbContext,
        TaskEventPublisher taskEventPublisher,
        ILogger<TaskItemService> logger)
    {
        _appDbContext = appDbContext;
        _taskEventPublisher = taskEventPublisher;
        _logger = logger;
    }

    public async Task Create(CreateTaskItem model)
    {
        if (string.IsNullOrEmpty(model.Title))
        {
            throw new InvalidOperationException("Title должен быть заполнен.");
        }

        if (model.Title.Length > _maxTitleLength)
        {
            throw new InvalidOperationException($"Title должен быть равен или меньше {_maxTitleLength}.");
        }

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

    public async Task<IReadOnlyList<TaskItemDto>> GetAll(int limit, int offset)
    {
        var items = await _appDbContext.Set<TaskItem>()
            .Skip(offset)
            .Take(limit)
            .Select(x => new TaskItemDto
            {
                TaskId = x.TaskId,
                Title = x.Title,
                Priority = x.Priority,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt,
                IsCompleted = x.IsCompleted
            }).ToListAsync();

        return items;
    }

    public async Task Complete(Guid taskId)
    {
        var entity = await _appDbContext.Set<TaskItem>()
            .Where(x => x.TaskId == taskId)
            .Select(x => new
            {
                x.Id,
                x.Title,
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"TaskItem TaskId {taskId} не найдена.");

        var now = DateTime.UtcNow;

        await _appDbContext.Set<TaskItem>()
            .Where(x => !x.IsCompleted)
            .Where(x => x.TaskId == taskId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(y => y.CompletedAt, now)
                .SetProperty(y => y.IsCompleted, true));
        try
        {
            var taskCompleted = new TaskCompletedEvent
            {
                TaskId = taskId,
                CompletedAt = now,
                Title = entity.Title,
                Priority = Priority.High
            };

            await _taskEventPublisher.PublishTaskCompleted(taskCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"TaskItem TaskId {taskId} RabbitMQ недоступен.");
        }
    }

    public async Task Delete(Guid taskId)
    {
        await _appDbContext.Set<TaskItem>()
            .Where(x => x.TaskId == taskId)
            .ExecuteDeleteAsync();
    }
}
