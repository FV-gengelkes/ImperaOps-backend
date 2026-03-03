using FreightVis.Api.Contracts;
using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using FreightVis.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/incidents/{incidentId:guid}/attachments")]
public sealed class IncidentAttachmentsController : ControllerBase
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

    private readonly FreightVisDbContext _db;
    private readonly IStorageService _storage;

    public IncidentAttachmentsController(FreightVisDbContext db, IStorageService storage)
    {
        _db      = db;
        _storage = storage;
    }

    /// <summary>Returns all attachments for an incident, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> GetAttachments(
        Guid incidentId, CancellationToken ct)
    {
        var attachments = await _db.IncidentAttachments
            .AsNoTracking()
            .Where(a => a.IncidentId == incidentId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentDto(
                a.Id, a.IncidentId, a.FileName, a.ContentType,
                a.FileSizeBytes, a.UploadedByUserId, a.UploadedByDisplayName, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(attachments);
    }

    /// <summary>Uploads a file and attaches it to an incident.</summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<AttachmentDto>> Upload(
        Guid incidentId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (file.Length > MaxFileSizeBytes)
            return BadRequest("File exceeds the 25 MB limit.");

        var contentType = file.ContentType;
        if (!AllowedMimeTypes.Contains(contentType))
            return BadRequest($"File type '{contentType}' is not allowed.");

        var incident = await _db.Incidents.FindAsync([incidentId], ct);
        if (incident is null) return NotFound();

        var fileName   = Path.GetFileName(file.FileName);
        var sanitized  = SanitizeFileName(fileName);
        var storageKey = $"{incidentId}/{Guid.NewGuid()}/{sanitized}";

        await using var stream = file.OpenReadStream();
        await _storage.UploadAsync(storageKey, stream, contentType, ct);

        var (actorId, actorName) = ResolveActor();

        var attachment = new IncidentAttachment
        {
            Id                    = Guid.NewGuid(),
            IncidentId            = incidentId,
            ClientId              = incident.ClientId,
            FileName              = fileName,
            StorageKey            = storageKey,
            ContentType           = contentType,
            FileSizeBytes         = file.Length,
            UploadedByUserId      = actorId,
            UploadedByDisplayName = actorName,
            CreatedAt             = DateTimeOffset.UtcNow,
        };

        _db.IncidentAttachments.Add(attachment);

        _db.IncidentEvents.Add(new IncidentEvent
        {
            Id              = Guid.NewGuid(),
            IncidentId      = incidentId,
            ClientId        = incident.ClientId,
            EventType       = "attachment_added",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Attached: {fileName} ({FormatSize(file.Length)}).",
            CreatedAt       = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new AttachmentDto(
            attachment.Id, attachment.IncidentId, attachment.FileName,
            attachment.ContentType, attachment.FileSizeBytes,
            attachment.UploadedByUserId, attachment.UploadedByDisplayName, attachment.CreatedAt));
    }

    /// <summary>Returns a 15-minute presigned download URL for an attachment.</summary>
    [HttpGet("{attachmentId:guid}/url")]
    public async Task<ActionResult<AttachmentUrlDto>> GetUrl(
        Guid incidentId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await _db.IncidentAttachments.FindAsync([attachmentId], ct);
        if (attachment is null || attachment.IncidentId != incidentId) return NotFound();

        var url = await _storage.GetPresignedUrlAsync(attachment.StorageKey, TimeSpan.FromMinutes(15));
        return Ok(new AttachmentUrlDto(url));
    }

    /// <summary>Deletes an attachment. Only the uploader or a super-admin may delete.</summary>
    [Authorize]
    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid incidentId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await _db.IncidentAttachments.FindAsync([attachmentId], ct);
        if (attachment is null || attachment.IncidentId != incidentId) return NotFound();

        var (actorId, actorName) = ResolveActor();
        var isSuperAdmin         = User.FindFirstValue("is_super_admin") == "true";

        if (!isSuperAdmin && (actorId is null || attachment.UploadedByUserId != actorId))
            return StatusCode(403, "You can only delete your own attachments.");

        await _storage.DeleteAsync(attachment.StorageKey, ct);

        _db.IncidentAttachments.Remove(attachment);

        _db.IncidentEvents.Add(new IncidentEvent
        {
            Id              = Guid.NewGuid(),
            IncidentId      = incidentId,
            ClientId        = attachment.ClientId,
            EventType       = "attachment_removed",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Removed attachment: {attachment.FileName}.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private (Guid? Id, string Name) ResolveActor()
    {
        var isSuperAdmin = User.FindFirstValue("is_super_admin") == "true";
        var idStr        = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(idStr, out var id);
        var name = isSuperAdmin
            ? "FreightVis Admin"
            : User.FindFirstValue("display_name") ?? "Unknown";
        return (id == Guid.Empty ? null : id, name);
    }

    private static string SanitizeFileName(string name)
    {
        return string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_'));
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }
}
