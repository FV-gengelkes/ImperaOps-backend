namespace ImperaOps.Domain.Entities;

public sealed class WorkflowRule : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Trigger type: event.created, event.updated, event.status_changed,
    /// event.assigned, event.closed
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>When true, no further rules are evaluated after this one matches.</summary>
    public bool StopOnMatch { get; set; }

    /// <summary>JSON array of condition objects.</summary>
    public string ConditionsJson { get; set; } = "[]";

    /// <summary>JSON array of action objects.</summary>
    public string ActionsJson { get; set; } = "[]";

    public long? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
