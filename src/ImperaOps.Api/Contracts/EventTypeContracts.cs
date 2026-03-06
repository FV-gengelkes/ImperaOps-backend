namespace ImperaOps.Api.Contracts;

public sealed record EventTypeDto(
    long Id,
    long ClientId,
    string Name,
    int SortOrder,
    bool IsSystem,
    bool IsActive,
    int Count
);

public sealed record CreateEventTypeRequest(long ClientId, string Name);

public sealed record UpdateEventTypeRequest(long ClientId, string Name, int SortOrder, bool IsActive);
