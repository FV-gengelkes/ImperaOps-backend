using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:long}/documents")]
[Authorize]
public sealed class ClientDocumentsController : ScopedControllerBase
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/heic",
        "application/pdf", "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain", "text/csv",
    };

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "policy", "procedure", "checklist", "form", "manual", "other",
    };

    private const long MaxFileSizeBytes = 25L * 1024 * 1024;

    private readonly ImperaOpsDbContext _db;
    private readonly IStorageService _storage;

    public ClientDocumentsController(ImperaOpsDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientDocumentDto>>> GetAll(
        long clientId, [FromQuery] string? category, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var query = _db.ClientDocuments.AsNoTracking().Where(d => d.ClientId == clientId);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(d => d.Category == category);

        var docs = await query
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new ClientDocumentDto(
                d.Id, d.ClientId, d.Title, d.Description, d.Category,
                d.FileName, d.ContentType, d.FileSizeBytes,
                d.UploadedByUserId, d.UploadedByDisplayName, d.Version,
                d.CreatedAt, d.UpdatedAt))
            .ToListAsync(ct);

        return Ok(docs);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ClientDocumentDto>> GetById(long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var d = await _db.ClientDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (d is null) throw new NotFoundException();

        return Ok(new ClientDocumentDto(
            d.Id, d.ClientId, d.Title, d.Description, d.Category,
            d.FileName, d.ContentType, d.FileSizeBytes,
            d.UploadedByUserId, d.UploadedByDisplayName, d.Version,
            d.CreatedAt, d.UpdatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<ClientDocumentDto>> Upload(
        long clientId,
        IFormFile file,
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string category,
        CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        if (file is null || file.Length == 0) throw new ValidationException("No file provided.");
        if (file.Length > MaxFileSizeBytes) throw new ValidationException("File exceeds the 25 MB limit.");
        if (!AllowedMimeTypes.Contains(file.ContentType))
            throw new ValidationException($"File type '{file.ContentType}' is not allowed.");
        if (!ValidCategories.Contains(category))
            throw new ValidationException($"Invalid category '{category}'.");

        var fileName = Path.GetFileName(file.FileName);
        var sanitized = SanitizeFileName(fileName);
        var storageKey = $"documents/{clientId}/{Guid.NewGuid()}/{sanitized}";

        await using var stream = file.OpenReadStream();
        await _storage.UploadAsync(storageKey, stream, file.ContentType, ct);

        var (actorId, actorName) = ResolveActor();
        var now = DateTimeOffset.UtcNow;

        var doc = new ClientDocument
        {
            ClientId = clientId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Category = category.ToLowerInvariant(),
            StorageKey = storageKey,
            FileName = fileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedByUserId = actorId,
            UploadedByDisplayName = actorName,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.ClientDocuments.Add(doc);

        Audit.Record("document", 0, clientId, "document_uploaded",
            $"Uploaded document: {title.Trim()} ({FormatSize(file.Length)}).");

        await _db.SaveChangesAsync(ct);

        return Ok(new ClientDocumentDto(
            doc.Id, doc.ClientId, doc.Title, doc.Description, doc.Category,
            doc.FileName, doc.ContentType, doc.FileSizeBytes,
            doc.UploadedByUserId, doc.UploadedByDisplayName, doc.Version,
            doc.CreatedAt, doc.UpdatedAt));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateMetadata(
        long clientId, long id, [FromBody] UpdateDocumentRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var doc = await _db.ClientDocuments.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (doc is null) throw new NotFoundException();

        if (!ValidCategories.Contains(req.Category))
            throw new ValidationException($"Invalid category '{req.Category}'.");

        doc.Title = req.Title.Trim();
        doc.Description = req.Description?.Trim();
        doc.Category = req.Category.ToLowerInvariant();
        doc.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:long}/file")]
    public async Task<ActionResult<ClientDocumentDto>> ReplaceFile(
        long clientId, long id, IFormFile file, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var doc = await _db.ClientDocuments.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (doc is null) throw new NotFoundException();

        if (file is null || file.Length == 0) throw new ValidationException("No file provided.");
        if (file.Length > MaxFileSizeBytes) throw new ValidationException("File exceeds the 25 MB limit.");
        if (!AllowedMimeTypes.Contains(file.ContentType))
            throw new ValidationException($"File type '{file.ContentType}' is not allowed.");

        // Delete old file
        await _storage.DeleteAsync(doc.StorageKey, ct);

        var fileName = Path.GetFileName(file.FileName);
        var sanitized = SanitizeFileName(fileName);
        var storageKey = $"documents/{clientId}/{Guid.NewGuid()}/{sanitized}";

        await using var stream = file.OpenReadStream();
        await _storage.UploadAsync(storageKey, stream, file.ContentType, ct);

        doc.StorageKey = storageKey;
        doc.FileName = fileName;
        doc.ContentType = file.ContentType;
        doc.FileSizeBytes = file.Length;
        doc.Version += 1;
        doc.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new ClientDocumentDto(
            doc.Id, doc.ClientId, doc.Title, doc.Description, doc.Category,
            doc.FileName, doc.ContentType, doc.FileSizeBytes,
            doc.UploadedByUserId, doc.UploadedByDisplayName, doc.Version,
            doc.CreatedAt, doc.UpdatedAt));
    }

    [HttpGet("{id:long}/url")]
    public async Task<ActionResult<object>> GetDownloadUrl(long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var doc = await _db.ClientDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (doc is null) throw new NotFoundException();

        var url = await _storage.GetPresignedUrlAsync(doc.StorageKey, TimeSpan.FromMinutes(15));
        return Ok(new { url });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var doc = await _db.ClientDocuments.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (doc is null) throw new NotFoundException();

        doc.DeletedAt = DateTimeOffset.UtcNow;

        Audit.Record("document", doc.Id, clientId, "document_deleted",
            $"Deleted document: {doc.Title}.");

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string SanitizeFileName(string name)
        => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_'));

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }
}
