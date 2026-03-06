namespace ImperaOps.Api.Contracts;

public sealed record CustomFieldDto(
    long    Id,
    long    ClientId,
    string  Name,
    string  DataType,
    bool    IsRequired,
    int     SortOrder,
    bool    IsActive,
    string? Options
);

public sealed record CreateCustomFieldRequest(
    long    ClientId,
    string  Name,
    string  DataType,
    bool    IsRequired,
    string? Options
);

public sealed record UpdateCustomFieldRequest(
    long    ClientId,
    string  Name,
    string  DataType,
    bool    IsRequired,
    int     SortOrder,
    string? Options
);

public sealed record CustomFieldValueDto(
    long    Id,
    long    EntityId,
    long    CustomFieldId,
    string  FieldName,
    string  DataType,
    string? Options,
    bool    IsRequired,
    string  Value
);

public sealed record FieldValueEntry(long CustomFieldId, string Value);

public sealed record UpsertCustomFieldValuesRequest(
    long                           EntityId,
    long                           ClientId,
    IReadOnlyList<FieldValueEntry> Values
);
