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

public sealed record CreateCommentRequest(string Body);
