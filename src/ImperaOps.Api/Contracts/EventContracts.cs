namespace ImperaOps.Api.Contracts;

public sealed record CreateEventRequest(
    long ClientId,
    long EventTypeId,
    long WorkflowStatusId,
    string Title,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    long? ReportedByUserId
);

public sealed record UpdateEventRequest(
    long EventTypeId,
    long WorkflowStatusId,
    string Title,
    DateTimeOffset OccurredAt,
    string Location,
    string Description,
    long? OwnerUserId,
    long? RootCauseId = null,
    string? CorrectiveAction = null
);

public sealed record BulkUpdateEventRequest(
    long ClientId,
    IReadOnlyList<long> EventIds,
    long? WorkflowStatusId,
    long? OwnerUserId,
    bool ClearOwner = false
);

public sealed record BulkDeleteEventRequest(long ClientId, string[] EventPublicIds);

public sealed record CreateEventResponse(long EventId, string PublicId);

public sealed record WorkloadRowDto(
    long? UserId,
    string UserName,
    int OpenEvents,
    int OpenTasks
);
