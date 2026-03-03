using MediatR;

namespace FreightVis.Application.Incidents.Commands;

public sealed record UpdateIncidentCommand(
    Guid Id,
    int Type,
    int Status,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    Guid? OwnerUserId
) : IRequest<Unit>;
