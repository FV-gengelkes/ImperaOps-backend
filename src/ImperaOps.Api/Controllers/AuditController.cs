using System.Text.RegularExpressions;
using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/events/{publicId}/audit")]
public sealed class AuditController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly INotificationService _notifications;

    public AuditController(ImperaOpsDbContext db, INotificationService notifications)
    {
        _db            = db;
        _notifications = notifications;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditEventDto>>> GetAuditLog(
        string publicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);

        var events = await _db.AuditEvents
            .AsNoTracking()
            .Where(a => a.EntityType == "event" && a.EntityId == ev.Id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AuditEventDto(
                a.Id, a.ClientId, a.EntityType, a.EntityId, a.EventType,
                a.UserId, a.UserDisplayName, a.Body, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(events);
    }

    [Authorize]
    [HttpPost("comments")]
    public async Task<ActionResult<AuditEventDto>> CreateComment(
        string publicId, [FromBody] CreateCommentRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Body))
            throw new ValidationException("Comment body is required.");

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) throw new ForbiddenException();

        var (actorId, actorName) = ResolveActor();

        Audit.Record("event", ev.Id, ev.ClientId, "comment", req.Body.Trim(), actorId, actorName);
        await _db.SaveChangesAsync(ct);

        // Retrieve the just-saved audit event to return its ID
        var saved = await _db.AuditEvents.AsNoTracking()
            .Where(a => a.EntityType == "event" && a.EntityId == ev.Id && a.EventType == "comment")
            .OrderByDescending(a => a.Id)
            .FirstAsync(ct);

        // Parse @[Name](userId) mention tokens
        var mentionedIds = new List<long>();
        foreach (Match m in Regex.Matches(req.Body, @"@\[([^\]]+)\]\((\d+)\)"))
            if (long.TryParse(m.Groups[2].Value, out var uid)) mentionedIds.Add(uid);

        await _notifications.NotifyCommentAddedAsync(
            ev.OwnerUserId ?? 0, ev.ReportedByUserId ?? 0,
            actorId ?? 0, actorName,
            ev.ClientId, publicId, ev.Title, req.Body.Trim(), mentionedIds, ct);

        return Ok(new AuditEventDto(
            saved.Id, saved.ClientId, saved.EntityType, saved.EntityId,
            saved.EventType, saved.UserId, saved.UserDisplayName, saved.Body, saved.CreatedAt));
    }

    [Authorize]
    [HttpDelete("{auditId:long}")]
    public async Task<IActionResult> DeleteAudit(
        string publicId, long auditId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);

        var audit = await _db.AuditEvents.FindAsync([auditId], ct);
        if (audit is null || audit.EntityType != "event" || audit.EntityId != ev.Id) throw new NotFoundException();
        if (audit.EventType != "comment") throw new ForbiddenException("Only comments can be deleted.");

        var (actorId, _) = ResolveActor();

        if (!IsSuperAdmin && (actorId is null || audit.UserId != actorId))
            throw new ForbiddenException("You can only delete your own comments.");

        audit.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

}
