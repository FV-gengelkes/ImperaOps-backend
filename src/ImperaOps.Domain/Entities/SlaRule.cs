namespace ImperaOps.Domain.Entities;

public sealed class SlaRule : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public long? EventTypeId { get; set; }       // null = applies to all event types
    public string Name { get; set; } = string.Empty;
    public int? InvestigationHours { get; set; } // null = no investigation SLA
    public int? ClosureHours { get; set; }       // null = no closure SLA
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
