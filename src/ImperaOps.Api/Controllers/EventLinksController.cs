using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dapper;
using MySqlConnector;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/event-links")]
[Authorize]
public sealed class EventLinksController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly string _cs;

    public EventLinksController(ImperaOpsDbContext db, IConfiguration config)
    {
        _db = db;
        _cs = config.GetConnectionString("Database")!;
    }

    // GET api/v1/event-links/groups?clientId=X
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups([FromQuery] long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        const string sql = @"
SELECT g.Id, g.ClientId, g.Title, g.Description, g.CreatedAt,
       (SELECT COUNT(1) FROM EventLinks l WHERE l.LinkGroupId = g.Id AND l.DeletedAt IS NULL) AS EventCount
FROM EventLinkGroups g
WHERE g.ClientId = @ClientId AND g.DeletedAt IS NULL
ORDER BY g.CreatedAt DESC;";

        await using var conn = new MySqlConnection(_cs);
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<EventLinkGroupDto>(new CommandDefinition(sql, new { ClientId = clientId }, cancellationToken: ct));
        return Ok(rows);
    }

    // GET api/v1/event-links/groups/{id}
    [HttpGet("groups/{id:long}")]
    public async Task<IActionResult> GetGroupDetail(long id, CancellationToken ct)
    {
        var group = await _db.EventLinkGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group == null) throw new NotFoundException();
        RequireClientAccess(group.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, group.ClientId, User, ct)) throw new ForbiddenException();

        const string sql = @"
SELECT l.EventId, e.PublicId, e.Title, et.Name AS EventTypeName,
       ws.Name AS WorkflowStatusName, ws.Color AS WorkflowStatusColor
FROM EventLinks l
JOIN Events e ON e.Id = l.EventId
LEFT JOIN EventTypes et ON et.Id = e.EventTypeId
LEFT JOIN WorkflowStatuses ws ON ws.Id = e.WorkflowStatusId
WHERE l.LinkGroupId = @GroupId AND l.DeletedAt IS NULL AND e.DeletedAt IS NULL
ORDER BY l.CreatedAt;";

        await using var conn = new MySqlConnection(_cs);
        await conn.OpenAsync(ct);
        var events = (await conn.QueryAsync<LinkedEventSummaryDto>(new CommandDefinition(sql, new { GroupId = id }, cancellationToken: ct))).ToList();

        return Ok(new EventLinkGroupDetailDto(
            group.Id, group.ClientId, group.Title, group.Description, events, group.CreatedAt.ToString("o")));
    }

    // POST api/v1/event-links/groups
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateEventLinkGroupRequest req, CancellationToken ct)
    {
        RequireClientAccess(req.ClientId);
        if (!await IsManagerOrAboveAsync(_db, req.ClientId, User, ct)) throw new ForbiddenException();

        var now = DateTimeOffset.UtcNow;
        var group = new EventLinkGroup
        {
            ClientId = req.ClientId,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim(),
            CreatedByUserId = CurrentUserId(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.EventLinkGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        if (req.EventIds is { Count: > 0 })
        {
            foreach (var eventId in req.EventIds)
            {
                _db.EventLinks.Add(new EventLink
                {
                    ClientId = req.ClientId,
                    LinkGroupId = group.Id,
                    EventId = eventId,
                    CreatedAt = now,
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { group.Id });
    }

    // PUT api/v1/event-links/groups/{id}
    [HttpPut("groups/{id:long}")]
    public async Task<IActionResult> UpdateGroup(long id, [FromBody] UpdateEventLinkGroupRequest req, CancellationToken ct)
    {
        var group = await _db.EventLinkGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group == null) throw new NotFoundException();
        RequireClientAccess(group.ClientId);
        if (!await IsManagerOrAboveAsync(_db, group.ClientId, User, ct)) throw new ForbiddenException();

        group.Title = req.Title.Trim();
        group.Description = req.Description?.Trim();
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE api/v1/event-links/groups/{id}
    [HttpDelete("groups/{id:long}")]
    public async Task<IActionResult> DeleteGroup(long id, CancellationToken ct)
    {
        var group = await _db.EventLinkGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group == null) throw new NotFoundException();
        RequireClientAccess(group.ClientId);
        if (!await IsAdminOfClientAsync(_db, group.ClientId, User, ct)) throw new ForbiddenException();

        var now = DateTimeOffset.UtcNow;
        group.DeletedAt = now;

        var links = await _db.EventLinks.Where(l => l.LinkGroupId == id).ToListAsync(ct);
        foreach (var l in links) l.DeletedAt = now;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // POST api/v1/event-links/groups/{id}/events
    [HttpPost("groups/{id:long}/events")]
    public async Task<IActionResult> AddEventToGroup(long id, [FromBody] AddEventToGroupRequest req, CancellationToken ct)
    {
        var group = await _db.EventLinkGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group == null) throw new NotFoundException();
        RequireClientAccess(group.ClientId);
        if (!await IsManagerOrAboveAsync(_db, group.ClientId, User, ct)) throw new ForbiddenException();

        var exists = await _db.EventLinks.AnyAsync(l => l.LinkGroupId == id && l.EventId == req.EventId, ct);
        if (exists) throw new ConflictException("Event already in group.");

        _db.EventLinks.Add(new EventLink
        {
            ClientId = group.ClientId,
            LinkGroupId = id,
            EventId = req.EventId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE api/v1/event-links/groups/{id}/events/{eventId}
    [HttpDelete("groups/{id:long}/events/{eventId:long}")]
    public async Task<IActionResult> RemoveEventFromGroup(long id, long eventId, CancellationToken ct)
    {
        var group = await _db.EventLinkGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group == null) throw new NotFoundException();
        RequireClientAccess(group.ClientId);
        if (!await IsManagerOrAboveAsync(_db, group.ClientId, User, ct)) throw new ForbiddenException();

        var link = await _db.EventLinks.FirstOrDefaultAsync(l => l.LinkGroupId == id && l.EventId == eventId, ct);
        if (link == null) throw new NotFoundException();

        link.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // GET api/v1/event-links/by-event/{publicId}
    [HttpGet("by-event/{publicId}")]
    public async Task<IActionResult> GetByEvent(string publicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) throw new ForbiddenException();

        const string sql = @"
SELECT g.Id, g.ClientId, g.Title, g.Description, g.CreatedAt,
       (SELECT COUNT(1) FROM EventLinks l2 WHERE l2.LinkGroupId = g.Id AND l2.DeletedAt IS NULL) AS EventCount
FROM EventLinkGroups g
JOIN EventLinks l ON l.LinkGroupId = g.Id
WHERE l.EventId = @EventId AND l.DeletedAt IS NULL AND g.DeletedAt IS NULL
ORDER BY g.CreatedAt DESC;";

        await using var conn = new MySqlConnection(_cs);
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<EventLinkGroupDto>(new CommandDefinition(sql, new { EventId = ev.Id }, cancellationToken: ct));
        return Ok(rows);
    }
}
