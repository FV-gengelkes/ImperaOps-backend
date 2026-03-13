namespace ImperaOps.Domain.Entities;

public sealed class Investigation : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public long EventId { get; set; }
    public string Status { get; set; } = "draft";
    public string? Summary { get; set; }
    public string? RootCauseAnalysis { get; set; }
    public string? CorrectiveActions { get; set; }
    public long? LeadInvestigatorUserId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
