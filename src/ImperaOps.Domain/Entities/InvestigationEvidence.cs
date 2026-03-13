namespace ImperaOps.Domain.Entities;

public sealed class InvestigationEvidence : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long InvestigationId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string EvidenceType { get; set; } = "other";
    public long? AttachmentId { get; set; }
    public long? CollectedByUserId { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
