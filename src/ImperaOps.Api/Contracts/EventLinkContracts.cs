namespace ImperaOps.Api.Contracts;

public sealed record EventLinkGroupDto(
    long Id,
    long ClientId,
    string Title,
    string? Description,
    int EventCount,
    string CreatedAt
);

public sealed record EventLinkGroupDetailDto(
    long Id,
    long ClientId,
    string Title,
    string? Description,
    IReadOnlyList<LinkedEventSummaryDto> Events,
    string CreatedAt
);

public sealed record LinkedEventSummaryDto(
    long EventId,
    string PublicId,
    string Title,
    string EventTypeName,
    string WorkflowStatusName,
    string? WorkflowStatusColor
);

public sealed record CreateEventLinkGroupRequest(
    long ClientId,
    string Title,
    string? Description,
    IReadOnlyList<long>? EventIds
);

public sealed record UpdateEventLinkGroupRequest(
    string Title,
    string? Description
);

public sealed record AddEventToGroupRequest(
    long EventId
);
