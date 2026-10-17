using Promoos.Core;

namespace Promoos.App.Dto;

public class TaskCompletedEvent
{
    public required Guid TaskId { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public required Priority Priority { get; set; }
}
