namespace ImperaOps.Domain.Entities;

public sealed class EventLinkGroup : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
