using Promoos.Core;

namespace Promoos.Tests.Dto;

public sealed record TaskDto(
    Guid TaskId,
    string Title,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Priority Priority);
