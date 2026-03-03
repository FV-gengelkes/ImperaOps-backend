namespace FreightVis.Application.Incidents.Dtos;

public sealed record IncidentExportRowDto(
    int ReferenceNumber,
    DateTime OccurredAt,
    string Type,
    string Status,
    string Location,
    string Description,
    string? Owner
);
