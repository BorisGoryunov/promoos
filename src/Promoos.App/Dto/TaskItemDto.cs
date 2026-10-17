using Promoos.Core;

namespace Promoos.App.Dto;

public class TaskItemDto
{
    public required Guid TaskId { get; init; }

    public required string Title { get; init; }

    public required bool IsCompleted { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset? CompletedAt { get; init; }

    public required Priority Priority { get; init; }
}
