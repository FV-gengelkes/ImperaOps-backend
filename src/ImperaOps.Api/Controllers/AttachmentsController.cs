using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/events/{publicId}/attachments")]
public sealed class AttachmentsController : ScopedControllerBase
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

    private const long MaxFileSizeBytes = 25L * 1024 * 1024; // 25 MB

    private readonly ImperaOpsDbContext _db;
    private readonly IStorageService _storage;

    public AttachmentsController(ImperaOpsDbContext db, IStorageService storage)
    {
        _db      = db;
        _storage = storage;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> GetAttachments(
        string publicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();

        var attachments = await _db.Attachments
            .AsNoTracking()
            .Where(a => a.EntityType == "event" && a.EntityId == ev.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentDto(
                a.Id, a.EntityType, a.EntityId, a.FileName, a.ContentType,
                a.FileSizeBytes, a.UploadedByUserId, a.UploadedByDisplayName, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(attachments);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<AttachmentDto>> Upload(
        string publicId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)     return BadRequest("No file provided.");
        if (file.Length > MaxFileSizeBytes)        return BadRequest("File exceeds the 25 MB limit.");
        if (!AllowedMimeTypes.Contains(file.ContentType))
            return BadRequest($"File type '{file.ContentType}' is not allowed.");

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) return Forbid();

        var fileName   = Path.GetFileName(file.FileName);
        var sanitized  = SanitizeFileName(fileName);
        var storageKey = $"events/{ev.Id}/{Guid.NewGuid()}/{sanitized}";

        await using var stream = file.OpenReadStream();
        await _storage.UploadAsync(storageKey, stream, file.ContentType, ct);

        var (actorId, actorName) = ResolveActor();

        var attachment = new Attachment
        {
            ClientId              = ev.ClientId,
            EntityType            = "event",
            EntityId              = ev.Id,
            FileName              = fileName,
            StorageKey            = storageKey,
            ContentType           = file.ContentType,
            FileSizeBytes         = file.Length,
            UploadedByUserId      = actorId,
            UploadedByDisplayName = actorName,
            CreatedAt             = DateTimeOffset.UtcNow,
        };

        _db.Attachments.Add(attachment);

        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = ev.ClientId,
            EntityType      = "event",
            EntityId        = ev.Id,
            EventType       = "attachment_added",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Attached: {fileName} ({FormatSize(file.Length)}).",
            CreatedAt       = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new AttachmentDto(
            attachment.Id, attachment.EntityType, attachment.EntityId,
            attachment.FileName, attachment.ContentType, attachment.FileSizeBytes,
            attachment.UploadedByUserId, attachment.UploadedByDisplayName, attachment.CreatedAt));
    }

    [Authorize]
    [HttpGet("{attachmentId:long}/url")]
    public async Task<ActionResult<AttachmentUrlDto>> GetUrl(
        string publicId, long attachmentId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();

        var attachment = await _db.Attachments.FindAsync([attachmentId], ct);
        if (attachment is null || attachment.EntityId != ev.Id) return NotFound();

        var url = await _storage.GetPresignedUrlAsync(attachment.StorageKey, TimeSpan.FromMinutes(15));
        return Ok(new AttachmentUrlDto(url));
    }

    [Authorize]
    [HttpDelete("{attachmentId:long}")]
    public async Task<IActionResult> Delete(
        string publicId, long attachmentId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();

        var attachment = await _db.Attachments.FindAsync([attachmentId], ct);
        if (attachment is null || attachment.EntityId != ev.Id) return NotFound();

        var (actorId, actorName) = ResolveActor();

        var isManagerPlus = await IsManagerOrAboveAsync(_db, ev.ClientId, User, ct);
        if (!isManagerPlus && (actorId is null || attachment.UploadedByUserId != actorId))
            return StatusCode(403, "You can only delete your own attachments.");

        await _storage.DeleteAsync(attachment.StorageKey, ct);
        attachment.DeletedAt = DateTimeOffset.UtcNow;

        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = ev.ClientId,
            EntityType      = "event",
            EntityId        = ev.Id,
            EventType       = "attachment_removed",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Removed attachment: {attachment.FileName}.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });

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
