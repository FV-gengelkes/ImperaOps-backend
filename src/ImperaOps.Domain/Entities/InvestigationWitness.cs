namespace ImperaOps.Domain.Entities;

public sealed class InvestigationWitness : ISoftDeletable
{
    public long Id { get; set; }
    public long InvestigationId { get; set; }
    public string WitnessName { get; set; } = "";
    public string? WitnessContact { get; set; }
    public string Statement { get; set; } = "";
    public DateTimeOffset? StatementDate { get; set; }
    public long? RecordedByUserId { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
