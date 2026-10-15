using Promoos.Core;

namespace Promoos.Entities;

public class TaskItem
{
    public int Id { get; set; }

    public required Guid TaskId { get; init; }

    public required string Title { get; init; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Priority Priority { get; set; }
}
