namespace ImperaOps.Domain.Entities;

public sealed class WorkflowStatus : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    /// <summary>0 = system default, otherwise a specific client.</summary>
    public long ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Hex color, e.g. "#16A34A".</summary>
    public string? Color { get; set; }
    public bool IsClosed { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
