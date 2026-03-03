namespace FreightVis.Api.Contracts;

public sealed record IncidentLookupDto(
    Guid   Id,
    Guid   ClientId,
    string FieldKey,
    string Label,
    int    Value,
    int    SortOrder,
    bool   IsSystem,
    bool   IsActive,
    int    Count
);

public sealed record CreateLookupRequest(
    Guid   ClientId,
    string FieldKey,
    string Label
);

public sealed record UpdateLookupRequest(
    Guid   ClientId,
    string Label,
    int    SortOrder
);
