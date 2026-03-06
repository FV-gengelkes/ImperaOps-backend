namespace ImperaOps.Api.Contracts;

public sealed record WorkflowTransitionDto(
    long Id,
    long ClientId,
    long? FromStatusId,
    long ToStatusId,
    long? EventTypeId,
    bool IsDefault,
    string? Label,
    DateTimeOffset CreatedAt
);

public sealed record CreateWorkflowTransitionRequest(
    long ClientId,
    long? FromStatusId,
    long ToStatusId,
    long? EventTypeId,
    bool IsDefault,
    string? Label
);
