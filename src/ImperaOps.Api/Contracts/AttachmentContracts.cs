namespace ImperaOps.Api.Contracts;

public sealed record AttachmentDto(
    long           Id,
    string         EntityType,
    long           EntityId,
    string         FileName,
    string         ContentType,
    long           FileSizeBytes,
    long?          UploadedByUserId,
    string         UploadedByDisplayName,
    DateTimeOffset CreatedAt);

public sealed record AttachmentUrlDto(string Url);
