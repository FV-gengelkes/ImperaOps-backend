using MediatR;

namespace FreightVis.Application.Incidents.Commands;

public sealed record CreateIncidentCommand(
    Guid ClientId,
    int Type,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    Guid ReportedByUserId,
    int ReferenceNumber
) : IRequest<CreateIncidentResult>;

public sealed record CreateIncidentResult(Guid IncidentId);
