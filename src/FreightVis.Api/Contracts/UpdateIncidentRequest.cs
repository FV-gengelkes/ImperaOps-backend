namespace FreightVis.Api.Contracts;

public sealed record UpdateIncidentRequest(
    int Type,
    int Status,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    Guid? OwnerUserId
);
