namespace FreightVis.Api.Contracts;

public sealed record CreateIncidentRequest(
    Guid ClientId,
    int Type,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    Guid ReportedByUserId
);

public sealed record CreateIncidentResponse(Guid IncidentId, int ReferenceNumber);
