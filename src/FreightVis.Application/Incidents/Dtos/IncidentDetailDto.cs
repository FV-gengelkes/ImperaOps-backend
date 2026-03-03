namespace FreightVis.Application.Incidents.Dtos;

public sealed record IncidentDetailDto(
    Guid Id,
    Guid ClientId,
    int Type,
    int Status,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    Guid ReportedByUserId,
    Guid? OwnerUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int ReferenceNumber,
    string? ReportedByDisplayName
);
