using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

public sealed record MyTaskDto(
    string TaskPublicId,
    string Title,
    string? Description,
    DateTimeOffset DueAt,
    string EventPublicId,
    string EventTitle
);

[ApiController]
[Route("api/v1/tasks")]
public sealed class MyTasksController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public MyTasksController(ImperaOpsDbContext db) => _db = db;

    [Authorize]
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<MyTaskDto>>> GetMyTasks(
        [FromQuery] long clientId,
        [FromQuery] int daysAhead = 14,
        CancellationToken ct = default)
    {
        var actorId = CurrentUserId();
        RequireClientAccess(clientId);

        daysAhead = Math.Clamp(daysAhead, 0, 365);

        var cutoff = DateTimeOffset.UtcNow.AddDays(daysAhead);

        var rows = await _db.Tasks
            .AsNoTracking()
            .Where(t =>
                t.ClientId == clientId &&
                t.AssignedToUserId == actorId &&
                !t.IsComplete &&
                t.DueAt != null &&
                t.DueAt <= cutoff)
            .Join(_db.Events,
                t => t.EventId,
                e => e.Id,
                (t, e) => new {
                    TaskPublicId  = t.PublicId,
                    t.Title,
                    t.Description,
                    DueAt         = t.DueAt!.Value,
                    EventPublicId = e.PublicId,
                    EventTitle    = e.Title,
                })
            .OrderBy(x => x.DueAt)
            .ToListAsync(ct);

        var items = rows.Select(x => new MyTaskDto(
            x.TaskPublicId, x.Title, x.Description,
            x.DueAt, x.EventPublicId, x.EventTitle)).ToList();

        return Ok(items);
    }
}
