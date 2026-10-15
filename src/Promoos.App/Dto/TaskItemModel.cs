using Promoos.Core;

namespace Promoos.App.Dto;

public class TaskItemModel
{
    public required Guid TaskId { get; init; }

    public required string Title { get; init; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Priority Priority { get; set; }
}
