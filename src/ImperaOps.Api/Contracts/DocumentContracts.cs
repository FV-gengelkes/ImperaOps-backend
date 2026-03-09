namespace ImperaOps.Api.Contracts;

public sealed record ClientDocumentDto(
    long Id,
    long ClientId,
    string Title,
    string? Description,
    string Category,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    long? UploadedByUserId,
    string UploadedByDisplayName,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record UpdateDocumentRequest(
    string Title,
    string? Description,
    string Category
);

public sealed record DocumentReferenceDto(
    long Id,
    long DocumentId,
    string DocumentTitle,
    string DocumentCategory,
    string DocumentFileName,
    long? CreatedByUserId,
    DateTimeOffset CreatedAt
);
