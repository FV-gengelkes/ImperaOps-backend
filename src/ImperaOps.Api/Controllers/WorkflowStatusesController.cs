using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/workflow-statuses")]
public sealed class WorkflowStatusesController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public WorkflowStatusesController(ImperaOpsDbContext db) => _db = db;

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowStatusDto>>> GetStatuses(
        [FromQuery] long clientId, CancellationToken ct)
    {
        if (clientId == 0) return BadRequest("clientId is required.");
        if (!HasClientAccess(clientId)) return NotFound();

        var rows = await _db.WorkflowStatuses
            .AsNoTracking()
            .Where(s => (s.ClientId == 0 || s.ClientId == clientId) && s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);

        var counts = await _db.Events
            .Where(e => e.ClientId == clientId)
            .GroupBy(e => e.WorkflowStatusId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var result = rows.Select(s => new WorkflowStatusDto(
            s.Id, s.ClientId, s.Name, s.Color, s.IsClosed, s.SortOrder, s.IsSystem, s.IsActive,
            counts.TryGetValue(s.Id, out var cnt) ? cnt : 0)).ToList();

        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<WorkflowStatusDto>> CreateStatus(
        [FromBody] CreateWorkflowStatusRequest req, CancellationToken ct)
    {
        if (req.ClientId == 0)                    return BadRequest("clientId is required.");
        if (!HasClientAccess(req.ClientId))        return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name))  return BadRequest("Name is required.");

        var maxOrder = await _db.WorkflowStatuses
            .Where(s => s.ClientId == req.ClientId || s.ClientId == 0)
            .Select(s => (int?)s.SortOrder)
            .MaxAsync(ct) ?? 0;

        var status = new WorkflowStatus
        {
            ClientId  = req.ClientId,
            Name      = req.Name.Trim(),
            Color     = req.Color,
            IsClosed  = req.IsClosed,
            SortOrder = maxOrder + 1,
            IsSystem  = false,
            IsActive  = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.WorkflowStatuses.Add(status);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetStatuses),
            new WorkflowStatusDto(status.Id, status.ClientId, status.Name, status.Color, status.IsClosed,
                status.SortOrder, status.IsSystem, status.IsActive, 0));
    }

    [Authorize]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateStatus(
        long id, [FromBody] UpdateWorkflowStatusRequest req, CancellationToken ct)
    {
        var status = await _db.WorkflowStatuses.FindAsync([id], ct);
        if (status is null || !HasClientAccess(status.ClientId)) return NotFound();
        if (status.IsSystem) return StatusCode(403, "System rows cannot be edited.");
        if (status.ClientId != req.ClientId) return StatusCode(403, "ClientId mismatch.");

        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");

        status.Name      = req.Name.Trim();
        status.Color     = req.Color;
        status.IsClosed  = req.IsClosed;
        status.SortOrder = req.SortOrder;
        status.IsActive  = req.IsActive;
        status.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteStatus(long id, [FromQuery] long clientId, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();

        var status = await _db.WorkflowStatuses.FindAsync([id], ct);
        if (status is null) return NotFound();
        if (status.IsSystem) return StatusCode(403, "System rows cannot be deleted.");
        if (status.ClientId != clientId) return StatusCode(403, "ClientId mismatch.");

        var count = await _db.Events.CountAsync(e => e.ClientId == clientId && e.WorkflowStatusId == id, ct);
        if (count > 0)
            return Conflict($"Cannot delete: {count} event(s) use this status.");

        status.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
