using MediatR;

namespace ImperaOps.Application.Events.Commands;

public sealed record UpdateEventCommand(
    long Id,
    long EventTypeId,
    long WorkflowStatusId,
    string Title,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    long? OwnerUserId,
    long? RootCauseId = null,
    string? CorrectiveAction = null
) : IRequest<Unit>;
