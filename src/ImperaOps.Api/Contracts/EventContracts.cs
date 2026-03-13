using System.ComponentModel.DataAnnotations;

namespace ImperaOps.Api.Contracts;

public sealed record CreateEventRequest(
    long ClientId,
    long EventTypeId,
    long WorkflowStatusId,
    [StringLength(500, ErrorMessage = "Title must be 500 characters or fewer.")] string Title,
    DateTimeOffset OccurredAt,
    [StringLength(256, ErrorMessage = "Location must be 256 characters or fewer.")] string Location,
    [StringLength(5000, ErrorMessage = "Description must be 5,000 characters or fewer.")] string Description,
    long? ReportedByUserId
);

public sealed record UpdateEventRequest(
    long EventTypeId,
    long WorkflowStatusId,
    [StringLength(500, ErrorMessage = "Title must be 500 characters or fewer.")] string Title,
    DateTimeOffset OccurredAt,
    [StringLength(256, ErrorMessage = "Location must be 256 characters or fewer.")] string Location,
    [StringLength(5000, ErrorMessage = "Description must be 5,000 characters or fewer.")] string Description,
    long? OwnerUserId,
    long? RootCauseId = null,
    [StringLength(500, ErrorMessage = "Corrective action must be 500 characters or fewer.")] string? CorrectiveAction = null
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
