namespace ImperaOps.Domain.Entities;

public sealed class DocumentReference : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public long DocumentId { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
