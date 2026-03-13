using System.ComponentModel.DataAnnotations;

namespace ImperaOps.Api.Contracts;

public sealed record AuditEventDto(
    long Id,
    long ClientId,
    string EntityType,
    long EntityId,
    string EventType,
    long? UserId,
    string UserDisplayName,
    string Body,
    DateTimeOffset CreatedAt
);

public sealed record CreateCommentRequest(
    [StringLength(10000, ErrorMessage = "Comment must be 10,000 characters or fewer.")] string Body
);
