namespace FreightVis.Domain.Entities;

public sealed class IncidentAttachment
{
    public Guid   Id                    { get; set; }
    public Guid   IncidentId            { get; set; }
    public Guid   ClientId              { get; set; }
    public string FileName              { get; set; } = string.Empty;
    public string StorageKey            { get; set; } = string.Empty;
    public string ContentType           { get; set; } = string.Empty;
    public long   FileSizeBytes         { get; set; }
    public Guid?  UploadedByUserId      { get; set; }
    public string UploadedByDisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt     { get; set; }
}
