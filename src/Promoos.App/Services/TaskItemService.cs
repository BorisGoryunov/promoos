using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Promoos.App.Dto;
using Promoos.Core;
using Promoos.Data;
using Promoos.Entities;
using System.ComponentModel.DataAnnotations;

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

    public async Task<Guid> Create(CreateTaskItem model)
    {
        var priority = model.Priority ?? Priority.Medium;

        if (!Enum.IsDefined(priority))
        {
            throw new ValidationException($"Недопустимое значение Priority: {(int)priority}.");
        }

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
            TaskId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Priority = priority,
            Title = model.Title
        };

        await _appDbContext.AddAsync(entity);
        await _appDbContext.SaveChangesAsync();

        return entity.TaskId;
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
                x.IsCompleted
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"TaskItem TaskId {taskId} не найдена.");

        if (entity.IsCompleted)
        {
            throw new ConflictException($"TaskItem TaskId {taskId} конфликт.");
        }

        var now = DateTime.UtcNow;

        var rowCount = await _appDbContext.Set<TaskItem>()
            .Where(x => !x.IsCompleted)
            .Where(x => x.TaskId == taskId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(y => y.CompletedAt, now)
                .SetProperty(y => y.IsCompleted, true));

        if (rowCount == 0)
        {
            throw new ConflictException($"TaskItem TaskId {taskId} конфликт.");
        }

        try
        {
            var taskCompletedEvent = new TaskCompletedEvent
            {
                TaskId = taskId,
                CompletedAt = now,
                Title = entity.Title,
                Priority = Priority.High
            };

            await _taskEventPublisher.PublishTaskCompleted(taskCompletedEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"TaskItem TaskId {taskId} RabbitMQ недоступен.");
        }
    }

    public async Task Delete(Guid taskId)
    {
        var deleteCount = await _appDbContext.Set<TaskItem>()
            .Where(x => x.TaskId == taskId)
            .ExecuteDeleteAsync();

        if (deleteCount == 0)
        {
            throw new KeyNotFoundException();
        }
    }
}
