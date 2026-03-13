using System.ComponentModel.DataAnnotations;

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
    [StringLength(500, ErrorMessage = "Task title must be 500 characters or fewer.")] string Title,
    [StringLength(5000, ErrorMessage = "Task description must be 5,000 characters or fewer.")] string? Description,
    long? AssignedToUserId,
    DateTimeOffset? DueAt
);

public sealed record UpdateTaskRequest(
    [StringLength(500, ErrorMessage = "Task title must be 500 characters or fewer.")] string Title,
    [StringLength(5000, ErrorMessage = "Task description must be 5,000 characters or fewer.")] string? Description,
    long? AssignedToUserId,
    DateTimeOffset? DueAt
);

public sealed record ReorderTasksRequest(IReadOnlyList<string> OrderedPublicIds);
