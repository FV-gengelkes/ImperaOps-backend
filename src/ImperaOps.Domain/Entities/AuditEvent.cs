namespace ImperaOps.Domain.Entities;

public sealed class AuditEvent : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    /// <summary>"event" | "task"</summary>
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    /// <summary>"created","type_changed","status_changed","owner_changed","comment","task_added","task_completed"</summary>
    public string EventType { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
