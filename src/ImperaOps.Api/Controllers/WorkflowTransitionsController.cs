using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/workflow-transitions")]
public sealed class WorkflowTransitionsController : ControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public WorkflowTransitionsController(ImperaOpsDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowTransitionDto>>> GetTransitions(
        [FromQuery] long clientId, CancellationToken ct)
    {
        if (clientId == 0) throw new ValidationException("clientId is required.");

        var hasClientTransitions = await _db.WorkflowTransitions
            .AnyAsync(t => t.ClientId == clientId, ct);

        var rows = await _db.WorkflowTransitions
            .AsNoTracking()
            .Where(t => hasClientTransitions ? t.ClientId == clientId : (t.ClientId == 0 || t.ClientId == clientId))
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        return Ok(rows.Select(t => new WorkflowTransitionDto(
            t.Id, t.ClientId, t.FromStatusId, t.ToStatusId,
            t.EventTypeId, t.IsDefault, t.Label, t.CreatedAt)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowTransitionDto>> CreateTransition(
        [FromBody] CreateWorkflowTransitionRequest req, CancellationToken ct)
    {
        if (req.ClientId == 0) throw new ValidationException("clientId is required.");

        var transition = new WorkflowTransition
        {
            ClientId     = req.ClientId,
            FromStatusId = req.FromStatusId,
            ToStatusId   = req.ToStatusId,
            EventTypeId  = req.EventTypeId,
            IsDefault    = req.IsDefault,
            Label        = req.Label?.Trim(),
            CreatedAt    = DateTimeOffset.UtcNow,
        };

        _db.WorkflowTransitions.Add(transition);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTransitions),
            new WorkflowTransitionDto(transition.Id, transition.ClientId, transition.FromStatusId,
                transition.ToStatusId, transition.EventTypeId, transition.IsDefault,
                transition.Label, transition.CreatedAt));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteTransition(long id, CancellationToken ct)
    {
        var transition = await _db.WorkflowTransitions.FindAsync([id], ct);
        if (transition is null) throw new NotFoundException();

        transition.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
