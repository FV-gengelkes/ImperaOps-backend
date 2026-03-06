namespace ImperaOps.Domain.Entities;

public sealed class NotificationPreference : ISoftDeletable
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string NotificationType { get; set; } = "";   // max 50
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
}
