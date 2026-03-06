namespace ImperaOps.Domain.Entities;

public sealed class EventType : ISoftDeletable
{
    public long Id { get; set; }
    /// <summary>0 = system default, otherwise a specific client.</summary>
    public long ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
