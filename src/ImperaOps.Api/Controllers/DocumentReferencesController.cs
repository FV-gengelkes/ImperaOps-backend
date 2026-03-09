using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/events/{publicId}/documents")]
[Authorize]
public sealed class DocumentReferencesController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public DocumentReferencesController(ImperaOpsDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentReferenceDto>>> GetAll(
        string publicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) throw new ForbiddenException();

        var refs = await _db.DocumentReferences.AsNoTracking()
            .Where(r => r.EntityType == "event" && r.EntityId == ev.Id)
            .Join(_db.ClientDocuments.AsNoTracking(),
                r => r.DocumentId,
                d => d.Id,
                (r, d) => new DocumentReferenceDto(
                    r.Id, r.DocumentId, d.Title, d.Category, d.FileName,
                    r.CreatedByUserId, r.CreatedAt))
            .ToListAsync(ct);

        return Ok(refs);
    }

    [HttpPost]
    public async Task<ActionResult<DocumentReferenceDto>> Link(
        string publicId, [FromBody] LinkDocumentRequest req, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) throw new ForbiddenException();

        var doc = await _db.ClientDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == req.DocumentId && d.ClientId == ev.ClientId, ct);
        if (doc is null) throw new ValidationException("Document not found.");

        // Check for duplicate
        var exists = await _db.DocumentReferences.AnyAsync(
            r => r.DocumentId == req.DocumentId && r.EntityType == "event" && r.EntityId == ev.Id, ct);
        if (exists) throw new ValidationException("Document already linked to this event.");

        var (actorId, _) = ResolveActor();
        var now = DateTimeOffset.UtcNow;

        var docRef = new DocumentReference
        {
            ClientId = ev.ClientId,
            DocumentId = req.DocumentId,
            EntityType = "event",
            EntityId = ev.Id,
            CreatedByUserId = actorId,
            CreatedAt = now,
        };

        _db.DocumentReferences.Add(docRef);
        await _db.SaveChangesAsync(ct);

        return Ok(new DocumentReferenceDto(
            docRef.Id, docRef.DocumentId, doc.Title, doc.Category, doc.FileName,
            docRef.CreatedByUserId, docRef.CreatedAt));
    }

    [HttpDelete("{refId:long}")]
    public async Task<IActionResult> Unlink(string publicId, long refId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);
        if (!await IsManagerOrAboveAsync(_db, ev.ClientId, User, ct)) throw new ForbiddenException();

        var docRef = await _db.DocumentReferences.FirstOrDefaultAsync(
            r => r.Id == refId && r.EntityType == "event" && r.EntityId == ev.Id, ct);
        if (docRef is null) throw new NotFoundException();

        docRef.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record LinkDocumentRequest(long DocumentId);
