namespace FreightVis.Api.Contracts;

public sealed record AttachmentDto(
    Guid            Id,
    Guid            IncidentId,
    string          FileName,
    string          ContentType,
    long            FileSizeBytes,
    Guid?           UploadedByUserId,
    string          UploadedByDisplayName,
    DateTimeOffset  CreatedAt);

public sealed record AttachmentUrlDto(string Url);
