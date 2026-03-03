using FreightVis.Api.Contracts;
using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/incidents/{incidentId:guid}/events")]
public sealed class IncidentEventsController : ControllerBase
{
    private readonly FreightVisDbContext _db;

    public IncidentEventsController(FreightVisDbContext db) => _db = db;

    /// <summary>Returns all events for an incident in chronological order.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentEventDto>>> GetEvents(
        Guid incidentId,
        CancellationToken ct)
    {
        var events = await _db.IncidentEvents
            .AsNoTracking()
            .Where(e => e.IncidentId == incidentId)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new IncidentEventDto(
                e.Id, e.IncidentId, e.EventType, e.UserId, e.UserDisplayName, e.Body, e.CreatedAt))
            .ToListAsync(ct);

        return Ok(events);
    }

    /// <summary>Posts a comment on an incident.</summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<IncidentEventDto>> CreateComment(
        Guid incidentId,
        [FromBody] CreateCommentRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Body))
            return BadRequest("Comment body is required.");

        var incident = await _db.Incidents.FindAsync([incidentId], ct);
        if (incident is null) return NotFound();

        var (actorId, actorName) = ResolveActor();

        var ev = new IncidentEvent
        {
            Id              = Guid.NewGuid(),
            IncidentId      = incidentId,
            ClientId        = incident.ClientId,
            EventType       = "comment",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = req.Body.Trim(),
            CreatedAt       = DateTimeOffset.UtcNow,
        };

        _db.IncidentEvents.Add(ev);
        await _db.SaveChangesAsync(ct);

        return Ok(new IncidentEventDto(
            ev.Id, ev.IncidentId, ev.EventType, ev.UserId, ev.UserDisplayName, ev.Body, ev.CreatedAt));
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

    /// <summary>Deletes a comment. Only the author or a super-admin may delete.</summary>
    [Authorize]
    [HttpDelete("{eventId:guid}")]
    public async Task<IActionResult> DeleteEvent(
        Guid incidentId,
        Guid eventId,
        CancellationToken ct)
    {
        var ev = await _db.IncidentEvents.FindAsync([eventId], ct);
        if (ev is null || ev.IncidentId != incidentId) return NotFound();
        if (ev.EventType != "comment") return StatusCode(403, "Only comments can be deleted.");

        var (actorId, _) = ResolveActor();
        var isSuperAdmin  = User.FindFirstValue("is_super_admin") == "true";

        if (!isSuperAdmin && (actorId is null || ev.UserId != actorId))
            return StatusCode(403, "You can only delete your own comments.");

        _db.IncidentEvents.Remove(ev);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
