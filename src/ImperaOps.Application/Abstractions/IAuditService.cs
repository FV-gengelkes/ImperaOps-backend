namespace ImperaOps.Application.Abstractions;

/// <summary>Centralized service for recording audit trail entries.</summary>
public interface IAuditService
{
    /// <summary>Records a single audit event. Does NOT call SaveChanges — caller must flush.</summary>
    void Record(string entityType, long entityId, long clientId, string eventType, string body);

    /// <summary>Records a single audit event with an explicit actor (for background jobs, etc.).</summary>
    void Record(string entityType, long entityId, long clientId, string eventType, string body,
                long? actorId, string actorName);
}
