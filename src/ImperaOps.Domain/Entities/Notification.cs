namespace ImperaOps.Domain.Entities;

public sealed class Notification : ISoftDeletable
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ClientId { get; set; }
    public string NotificationType { get; set; } = "";   // max 50
    public string Title { get; set; } = "";              // max 255
    public string Body { get; set; } = "";               // longtext
    public string? EntityPublicId { get; set; }          // max 20, e.g. "EVT-0042"
    public string? SubEntityPublicId { get; set; }       // max 20, e.g. "TSK-0001"
    public bool IsRead { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
