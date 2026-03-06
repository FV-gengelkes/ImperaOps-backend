using MediatR;

namespace ImperaOps.Application.Tasks;

public sealed record CreateTaskCommand(
    long ClientId,
    long EventId,
    string Title,
    string? Description,
    long? AssignedToUserId,
    DateTimeOffset? DueAt
) : IRequest<CreateTaskResult>;

public sealed record CreateTaskResult(long TaskId, string PublicId);

public sealed record UpdateTaskCommand(
    long Id,
    string Title,
    string? Description,
    long? AssignedToUserId,
    DateTimeOffset? DueAt
) : IRequest<Unit>;

public sealed record CompleteTaskCommand(long Id) : IRequest<Unit>;

public sealed record UncompleteTaskCommand(long Id) : IRequest<Unit>;

public sealed record DeleteTaskCommand(long Id) : IRequest<Unit>;
