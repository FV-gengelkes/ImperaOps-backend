using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;

namespace ImperaOps.Infrastructure.Services;

/// <summary>Adds audit events to the DbContext change tracker. Caller must call SaveChangesAsync.</summary>
public sealed class AuditService(ImperaOpsDbContext db, ICurrentUser currentUser) : IAuditService
{
    public void Record(string entityType, long entityId, long clientId, string eventType, string body)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = clientId,
            EntityType      = entityType,
            EntityId        = entityId,
            EventType       = eventType,
            UserId          = currentUser.Id == 0 ? null : currentUser.Id,
            UserDisplayName = currentUser.DisplayName,
            Body            = body,
            CreatedAt       = DateTimeOffset.UtcNow,
        });
    }

    public void Record(string entityType, long entityId, long clientId, string eventType, string body,
                       long? actorId, string actorName)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = clientId,
            EntityType      = entityType,
            EntityId        = entityId,
            EventType       = eventType,
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = body,
            CreatedAt       = DateTimeOffset.UtcNow,
        });
    }
}
