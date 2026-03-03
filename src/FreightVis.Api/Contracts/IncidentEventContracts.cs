namespace FreightVis.Api.Contracts;

public sealed record IncidentEventDto(
    Guid            Id,
    Guid            IncidentId,
    string          EventType,
    Guid?           UserId,
    string          UserDisplayName,
    string          Body,
    DateTimeOffset  CreatedAt
);

public sealed record CreateCommentRequest(string Body);
