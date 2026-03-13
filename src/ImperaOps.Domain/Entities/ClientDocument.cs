namespace ImperaOps.Domain.Entities;

public sealed class ClientDocument : ISoftDeletable, ISeedable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public long? UploadedByUserId { get; set; }
    public string UploadedByDisplayName { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsSeedData { get; set; }
}
