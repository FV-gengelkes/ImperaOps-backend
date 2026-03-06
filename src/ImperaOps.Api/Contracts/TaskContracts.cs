namespace ImperaOps.Api.Contracts;

public sealed record TaskDto(
    long Id,
    long ClientId,
    long EventId,
    string PublicId,
    string Title,
    string? Description,
    long? AssignedToUserId,
    string? AssignedToDisplayName,
    DateTimeOffset? DueAt,
    bool IsComplete,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    long? AssignedToUserId,
    DateTimeOffset? DueAt
);

public sealed record UpdateTaskRequest(
    string Title,
    string? Description,
    long? AssignedToUserId,
    DateTimeOffset? DueAt
);

public sealed record ReorderTasksRequest(IReadOnlyList<string> OrderedPublicIds);
