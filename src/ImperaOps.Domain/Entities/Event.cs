namespace ImperaOps.Domain.Entities;

public sealed class Event : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    /// <summary>Human-readable ID, e.g. "EVT-0001". Unique per client.</summary>
    public string PublicId { get; set; } = string.Empty;
    public long EventTypeId { get; set; }
    public long WorkflowStatusId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long? ReportedByUserId { get; set; }
    public string? ExternalReporterName { get; set; }
    public string? ExternalReporterContact { get; set; }
    public long? OwnerUserId { get; set; }
    /// <summary>Sequential per client. Used to generate PublicId.</summary>
    public long ReferenceNumber { get; set; }
    public long? RootCauseId { get; set; }
    public string? CorrectiveAction { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
