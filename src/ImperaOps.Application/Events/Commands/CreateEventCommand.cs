using MediatR;

namespace ImperaOps.Application.Events.Commands;

public sealed record CreateEventCommand(
    long ClientId,
    long EventTypeId,
    long WorkflowStatusId,
    string Title,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    long? ReportedByUserId,
    string? ExternalReporterName = null,
    string? ExternalReporterContact = null
) : IRequest<CreateEventResult>;

public sealed record CreateEventResult(long EventId, string PublicId);
