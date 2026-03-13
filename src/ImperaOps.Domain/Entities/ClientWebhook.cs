namespace ImperaOps.Domain.Entities;

public sealed class ClientWebhook : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Secret { get; set; }
    /// <summary>JSON array of event type strings, e.g. ["event.created","event.updated"]</summary>
    public string EventTypes { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
