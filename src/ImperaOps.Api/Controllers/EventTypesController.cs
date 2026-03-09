using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/event-types")]
public sealed class EventTypesController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public EventTypesController(ImperaOpsDbContext db) => _db = db;

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventTypeDto>>> GetEventTypes(
        [FromQuery] long clientId, CancellationToken ct)
    {
        if (clientId == 0) throw new ValidationException("clientId is required.");
        RequireClientAccess(clientId);

        var hasClientTypes = await _db.EventTypes
            .AnyAsync(t => t.ClientId == clientId && t.IsActive, ct);

        var rows = await _db.EventTypes
            .AsNoTracking()
            .Where(t => (hasClientTypes ? t.ClientId == clientId : (t.ClientId == 0 || t.ClientId == clientId)) && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .ToListAsync(ct);

        var counts = await _db.Events
            .Where(e => e.ClientId == clientId)
            .GroupBy(e => e.EventTypeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var result = rows.Select(t => new EventTypeDto(
            t.Id, t.ClientId, t.Name, t.SortOrder, t.IsSystem, t.IsActive,
            counts.TryGetValue(t.Id, out var cnt) ? cnt : 0)).ToList();

        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<EventTypeDto>> CreateEventType(
        [FromBody] CreateEventTypeRequest req, CancellationToken ct)
    {
        if (req.ClientId == 0)                    throw new ValidationException("clientId is required.");
        RequireClientAccess(req.ClientId);
        if (string.IsNullOrWhiteSpace(req.Name))  throw new ValidationException("Name is required.");

        var maxOrder = await _db.EventTypes
            .Where(t => t.ClientId == req.ClientId)
            .Select(t => (int?)t.SortOrder)
            .MaxAsync(ct) ?? 0;

        var type = new EventType
        {
            ClientId  = req.ClientId,
            Name      = req.Name.Trim(),
            SortOrder = maxOrder + 1,
            IsSystem  = false,
            IsActive  = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.EventTypes.Add(type);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetEventTypes),
            new EventTypeDto(type.Id, type.ClientId, type.Name, type.SortOrder, type.IsSystem, type.IsActive, 0));
    }

    [Authorize]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateEventType(
        long id, [FromBody] UpdateEventTypeRequest req, CancellationToken ct)
    {
        var type = await _db.EventTypes.FindAsync([id], ct);
        if (type is null || !HasClientAccess(type.ClientId)) throw new NotFoundException();
        if (type.IsSystem) throw new ForbiddenException("System rows cannot be edited.");
        if (type.ClientId != req.ClientId) throw new ForbiddenException("ClientId mismatch.");

        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");

        type.Name      = req.Name.Trim();
        type.SortOrder = req.SortOrder;
        type.IsActive  = req.IsActive;
        type.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteEventType(long id, [FromQuery] long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);

        var type = await _db.EventTypes.FindAsync([id], ct);
        if (type is null) throw new NotFoundException();
        if (type.IsSystem) throw new ForbiddenException("System rows cannot be deleted.");
        if (type.ClientId != clientId) throw new ForbiddenException("ClientId mismatch.");

        var count = await _db.Events.CountAsync(e => e.ClientId == clientId && e.EventTypeId == id, ct);
        if (count > 0)
            throw new ConflictException($"Cannot delete: {count} event(s) use this type.");

        type.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
