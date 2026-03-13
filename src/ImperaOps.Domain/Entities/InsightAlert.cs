namespace ImperaOps.Domain.Entities;

public sealed class InsightAlert : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string AlertType { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? MetadataJson { get; set; }
    public string? RelatedEventIds { get; set; }
    public bool IsAcknowledged { get; set; }
    public long? AcknowledgedByUserId { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? AiSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
