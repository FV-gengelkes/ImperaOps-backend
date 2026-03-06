using ImperaOps.Api.Contracts;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Tasks;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/events/{publicId}/tasks")]
public sealed class TasksController : ScopedControllerBase
{
    private readonly IMediator _mediator;
    private readonly ImperaOpsDbContext _db;
    private readonly INotificationService _notifications;

    public TasksController(IMediator mediator, ImperaOpsDbContext db, INotificationService notifications)
    {
        _mediator      = mediator;
        _db            = db;
        _notifications = notifications;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetTasks(
        string publicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();

        var tasks = await _db.Tasks
            .AsNoTracking()
            .Where(t => t.EventId == ev.Id)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);

        var userIds      = tasks.Where(t => t.AssignedToUserId.HasValue).Select(t => t.AssignedToUserId!.Value).Distinct().ToList();
        var userNames    = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return Ok(tasks.Select(t => new TaskDto(
            t.Id, t.ClientId, t.EventId, t.PublicId, t.Title, t.Description,
            t.AssignedToUserId,
            t.AssignedToUserId.HasValue ? userNames.GetValueOrDefault(t.AssignedToUserId.Value) : null,
            t.DueAt, t.IsComplete, t.SortOrder, t.CreatedAt, t.UpdatedAt)).ToList());
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<TaskDto>> CreateTask(
        string publicId, [FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");

        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) return Forbid();

        var result = await _mediator.Send(new CreateTaskCommand(
            ev.ClientId, ev.Id, req.Title, req.Description,
            req.AssignedToUserId, req.DueAt), ct);

        var (actorId, actorName) = ResolveActor();
        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = ev.ClientId,
            EntityType      = "event",
            EntityId        = ev.Id,
            EventType       = "task_added",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Task added: {req.Title}.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        if (req.AssignedToUserId.HasValue)
            await _notifications.NotifyTaskAssignedAsync(req.AssignedToUserId.Value, actorId ?? 0, actorName,
                ev.ClientId, publicId, result.PublicId, req.Title, ct);

        var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == result.TaskId, ct);
        return Ok(new TaskDto(
            task!.Id, task.ClientId, task.EventId, task.PublicId, task.Title, task.Description,
            task.AssignedToUserId, null, task.DueAt, task.IsComplete, task.SortOrder,
            task.CreatedAt, task.UpdatedAt));
    }

    [Authorize]
    [HttpPut("{taskPublicId}")]
    public async Task<IActionResult> UpdateTask(
        string publicId, string taskPublicId, [FromBody] UpdateTaskRequest req, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) return Forbid();

        var task = await _db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventId == ev.Id && t.PublicId == taskPublicId, ct);
        if (task is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");

        var (actorId, actorName) = ResolveActor();

        await _mediator.Send(new UpdateTaskCommand(
            task.Id, req.Title, req.Description, req.AssignedToUserId, req.DueAt), ct);

        if (!req.AssignedToUserId.HasValue && task.AssignedToUserId.HasValue)
        {
            // Unassigned — remove the existing notification
            await _notifications.ClearTaskNotificationAsync(taskPublicId, ct);
        }
        else if (req.AssignedToUserId.HasValue && req.AssignedToUserId != task.AssignedToUserId)
        {
            // Reassigned to a different user — replaces old notification automatically
            await _notifications.NotifyTaskAssignedAsync(req.AssignedToUserId.Value, actorId ?? 0, actorName,
                ev.ClientId, publicId, taskPublicId, req.Title, ct);
        }

        return NoContent();
    }

    [Authorize]
    [HttpPatch("{taskPublicId}/complete")]
    public async Task<IActionResult> CompleteTask(
        string publicId, string taskPublicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) return Forbid();

        var task = await _db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventId == ev.Id && t.PublicId == taskPublicId, ct);
        if (task is null) return NotFound();

        await _mediator.Send(new CompleteTaskCommand(task.Id), ct);

        var (actorId, actorName) = ResolveActor();
        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = ev.ClientId,
            EntityType      = "event",
            EntityId        = ev.Id,
            EventType       = "task_completed",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Task completed: {task.Title}.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [Authorize]
    [HttpPatch("{taskPublicId}/uncomplete")]
    public async Task<IActionResult> UncompleteTask(
        string publicId, string taskPublicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) return Forbid();

        var task = await _db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventId == ev.Id && t.PublicId == taskPublicId, ct);
        if (task is null) return NotFound();

        await _mediator.Send(new UncompleteTaskCommand(task.Id), ct);

        var (actorId, actorName) = ResolveActor();
        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = ev.ClientId,
            EntityType      = "event",
            EntityId        = ev.Id,
            EventType       = "task_reopened",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Task reopened: {task.Title}.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [Authorize]
    [HttpPatch("reorder")]
    public async Task<IActionResult> ReorderTasks(
        string publicId, [FromBody] ReorderTasksRequest req, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();

        var tasks = await _db.Tasks
            .Where(t => t.EventId == ev.Id)
            .ToListAsync(ct);

        int order = 0;
        foreach (var pid in req.OrderedPublicIds)
        {
            var task = tasks.FirstOrDefault(t => t.PublicId == pid);
            if (task is not null) task.SortOrder = order++;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{taskPublicId}")]
    public async Task<IActionResult> DeleteTask(
        string publicId, string taskPublicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) return Forbid();

        var task = await _db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EventId == ev.Id && t.PublicId == taskPublicId, ct);
        if (task is null) return NotFound();

        var (actorId, actorName) = ResolveActor();

        await _mediator.Send(new DeleteTaskCommand(task.Id), ct);
        await _notifications.ClearTaskNotificationAsync(taskPublicId, ct);

        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = ev.ClientId,
            EntityType      = "event",
            EntityId        = ev.Id,
            EventType       = "task_removed",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Task removed: {task.Title}.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}
