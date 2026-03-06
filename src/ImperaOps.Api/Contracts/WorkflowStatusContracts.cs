namespace ImperaOps.Api.Contracts;

public sealed record WorkflowStatusDto(
    long Id,
    long ClientId,
    string Name,
    string? Color,
    bool IsClosed,
    int SortOrder,
    bool IsSystem,
    bool IsActive,
    int Count
);

public sealed record CreateWorkflowStatusRequest(long ClientId, string Name, string? Color, bool IsClosed);

public sealed record UpdateWorkflowStatusRequest(long ClientId, string Name, string? Color, bool IsClosed, int SortOrder, bool IsActive);
