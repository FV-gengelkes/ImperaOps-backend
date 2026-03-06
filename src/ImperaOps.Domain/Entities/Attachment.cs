namespace ImperaOps.Domain.Entities;

public sealed class Attachment : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    /// <summary>"event" | "task"</summary>
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>S3 storage key.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long? UploadedByUserId { get; set; }
    public string UploadedByDisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
