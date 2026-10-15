using Promoos.Core;

namespace Promoos.App.Dto;

public class TaskCompletedEvent
{
    public required Guid TaskId { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Priority Priority { get; set; }
}
