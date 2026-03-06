namespace ImperaOps.Domain.Entities;

public sealed class WorkflowTransition : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    /// <summary>null = from any status.</summary>
    public long? FromStatusId { get; set; }
    public long ToStatusId { get; set; }
    /// <summary>null = applies to all event types.</summary>
    public long? EventTypeId { get; set; }
    public bool IsDefault { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
