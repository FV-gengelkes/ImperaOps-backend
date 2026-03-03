namespace FreightVis.Application.Incidents.Dtos;

public sealed record IncidentListItemDto(
    Guid Id,
    Guid ClientId,
    int Type,
    int Status,
    DateTime OccurredAt,
    string Location,
    Guid? OwnerUserId,
    int ReferenceNumber,
    string? OwnerDisplayName
);
