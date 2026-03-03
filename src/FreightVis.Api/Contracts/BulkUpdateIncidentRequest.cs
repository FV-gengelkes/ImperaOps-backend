namespace FreightVis.Api.Contracts;

public sealed record BulkUpdateIncidentRequest(
    Guid ClientId,
    List<Guid> IncidentIds,
    int? Status,
    Guid? OwnerUserId,
    bool ClearOwner   // true → set OwnerUserId = null; takes precedence over OwnerUserId
);
