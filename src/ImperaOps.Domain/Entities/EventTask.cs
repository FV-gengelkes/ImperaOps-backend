namespace ImperaOps.Domain.Entities;

/// <summary>A task attached to an event. Named EventTask to avoid conflict with System.Threading.Tasks.Task.</summary>
public sealed class EventTask : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public long EventId { get; set; }
    /// <summary>Human-readable ID per event, e.g. "TSK-0001".</summary>
    public string PublicId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? AssignedToUserId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public bool IsComplete { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
