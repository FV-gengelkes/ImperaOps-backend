namespace FreightVis.Api.Contracts;

public sealed record CustomFieldDto(
    Guid    Id,
    Guid    ClientId,
    string  Name,
    string  DataType,
    bool    IsRequired,
    int     SortOrder,
    bool    IsActive,
    string? Options
);

public sealed record CreateCustomFieldRequest(
    Guid    ClientId,
    string  Name,
    string  DataType,
    bool    IsRequired,
    string? Options
);

public sealed record UpdateCustomFieldRequest(
    Guid    ClientId,
    string  Name,
    string  DataType,
    bool    IsRequired,
    int     SortOrder,
    string? Options
);

public sealed record CustomFieldValueDto(
    Guid    Id,
    Guid    IncidentId,
    Guid    CustomFieldId,
    string  FieldName,
    string  DataType,
    string? Options,
    bool    IsRequired,
    string  Value
);

public sealed record FieldValueEntry(Guid CustomFieldId, string Value);

public sealed record UpsertCustomFieldValuesRequest(
    Guid                        IncidentId,
    Guid                        ClientId,
    IReadOnlyList<FieldValueEntry> Values
);
