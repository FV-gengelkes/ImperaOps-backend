using ImperaOps.Api.Contracts;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Commands;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Application.Events.Queries;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Notifications;
using ImperaOps.Infrastructure.Webhooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
public sealed class EventsController : ScopedControllerBase
{
    private readonly IMediator _mediator;
    private readonly ImperaOpsDbContext _db;
    private readonly IEventReadRepository _readRepo;
    private readonly INotificationService _notifications;
    private readonly IWebhookDispatcher _webhooks;
    private readonly IWorkflowEngine _workflows;

    public EventsController(IMediator mediator, ImperaOpsDbContext db, IEventReadRepository readRepo, INotificationService notifications, IWebhookDispatcher webhooks, IWorkflowEngine workflows)
    {
        _mediator      = mediator;
        _db            = db;
        _readRepo      = readRepo;
        _notifications = notifications;
        _webhooks      = webhooks;
        _workflows     = workflows;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResult<EventListItemDto>>> GetList(
        [FromQuery] long clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] long? eventTypeId = null,
        [FromQuery] long? workflowStatusId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? search = null,
        [FromQuery] bool slaBreached = false,
        [FromQuery] bool? isClosed = null,
        CancellationToken ct = default)
    {
        if (clientId == 0) throw new ValidationException("clientId is required.");
        RequireClientAccess(clientId);
        var res = await _mediator.Send(
            new GetEventListQuery(clientId, page, pageSize, eventTypeId, workflowStatusId, dateFrom, dateTo, search, slaBreached, isClosed), ct);
        return Ok(res);
    }

    [Authorize]
    [HttpGet("analytics")]
    public async Task<ActionResult<EventAnalyticsDto>> GetAnalytics(
        [FromQuery] List<long>? clientIds,
        [FromQuery] long clientId = default,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var ids = (clientIds?.Where(id => id != 0).ToList() is { Count: > 0 } list)
            ? list
            : clientId != 0 ? new List<long> { clientId } : null;

        if (ids is null || ids.Count == 0) throw new ValidationException("At least one clientId is required.");
        ids = ids.Where(HasClientAccess).ToList();
        if (ids.Count == 0) throw new NotFoundException();
        var res = await _mediator.Send(new GetEventAnalyticsQuery(ids, dateFrom, dateTo), ct);
        return Ok(res);
    }

    [Authorize]
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] long clientId,
        [FromQuery] long? eventTypeId,
        [FromQuery] long? workflowStatusId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        if (clientId == 0) throw new ValidationException("clientId is required.");
        RequireClientAccess(clientId);
        var rows     = await _readRepo.GetExportDataAsync(clientId, eventTypeId, workflowStatusId, dateFrom, dateTo, search, ct);
        var csv      = BuildCsv(rows);
        var filename = $"events-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", filename);
    }

    private static string BuildCsv(IReadOnlyList<EventExportRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Public ID,Occurred At,Type,Status,Location,Description,Owner");
        foreach (var r in rows)
            sb.AppendLine(string.Join(",",
                CsvEscape(r.PublicId),
                CsvEscape(r.OccurredAt.ToString("yyyy-MM-dd HH:mm")),
                CsvEscape(r.EventTypeName),
                CsvEscape(r.WorkflowStatusName),
                CsvEscape(r.Location),
                CsvEscape(r.Description),
                CsvEscape(r.Owner)));
        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    [Authorize]
    [HttpGet("workload")]
    public async Task<ActionResult<IReadOnlyList<Application.Events.Dtos.WorkloadRowDto>>> GetWorkload(
        [FromQuery] long clientId, CancellationToken ct = default)
    {
        if (clientId == 0) throw new ValidationException("clientId is required.");
        RequireClientAccess(clientId);
        var result = await _mediator.Send(new GetWorkloadQuery(clientId), ct);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("{publicId}")]
    public async Task<ActionResult<EventDetailDto>> GetDetail(
        [FromRoute] string publicId, [FromQuery] long clientId = 0, CancellationToken ct = default)
    {
        var res = await _mediator.Send(new GetEventDetailQuery(publicId, clientId > 0 ? clientId : null), ct);
        if (res is null || !HasClientAccess(res.ClientId)) throw new NotFoundException();
        return Ok(res);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CreateEventResponse>> Create(
        [FromBody] CreateEventRequest req, CancellationToken ct = default)
    {
        if (req.ClientId == 0)                            throw new ValidationException("ClientId is required.");
        RequireClientAccess(req.ClientId);
        if (!await IsManagerOrAboveAsync(_db, req.ClientId, User, ct)) throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(req.Title))         throw new ValidationException("Title is required.");
        if (string.IsNullOrWhiteSpace(req.Location))      throw new ValidationException("Location is required.");
        if (string.IsNullOrWhiteSpace(req.Description))   throw new ValidationException("Description is required.");

        var result = await _mediator.Send(new CreateEventCommand(
            req.ClientId, req.EventTypeId, req.WorkflowStatusId,
            req.Title, req.OccurredAt, req.Location, req.Description,
            req.ReportedByUserId), ct);

        // Log audit event
        Audit.Record("event", result.EventId, req.ClientId, "created", "Event reported.");
        await _db.SaveChangesAsync(ct);

        // Dispatch webhook (fire-and-forget)
        var createdEvent = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == result.EventId, ct);
        if (createdEvent is not null)
        {
            var eventTypeName  = await _db.EventTypes.AsNoTracking().Where(t => t.Id == createdEvent.EventTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct);
            var statusName     = await _db.WorkflowStatuses.AsNoTracking().Where(s => s.Id == createdEvent.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct);
            var statusIsClosed = await _db.WorkflowStatuses.AsNoTracking().Where(s => s.Id == createdEvent.WorkflowStatusId).Select(s => s.IsClosed).FirstOrDefaultAsync(ct);
            _ = _webhooks.DispatchAsync(req.ClientId, "event.created", BuildWebhookPayload("event.created", createdEvent, eventTypeName, statusName, statusIsClosed, null));
        }

        // Evaluate workflow rules (fire-and-forget, non-blocking)
        if (createdEvent is not null)
            _ = _workflows.EvaluateAsync("event.created", createdEvent, null, ct);

        return Ok(new CreateEventResponse(result.EventId, result.PublicId));
    }

    [Authorize]
    [HttpPut("{publicId}")]
    public async Task<IActionResult> Update(
        [FromRoute] string publicId,
        [FromBody] UpdateEventRequest req,
        [FromQuery] long clientId = 0,
        CancellationToken ct = default)
    {
        var query = _db.Events.AsNoTracking().Where(e => e.PublicId == publicId);
        if (clientId > 0)
            query = query.Where(e => e.ClientId == clientId);
        var existing = await query.FirstOrDefaultAsync(ct);

        if (existing is null || !HasClientAccess(existing.ClientId)) throw new NotFoundException();

        // Role gate: Manager+ can edit any; Investigator/Member only their own
        if (!IsSuperAdmin)
        {
            var role = await GetUserRoleAsync(_db, existing.ClientId, User, ct);
            if (role is "Admin" or "Manager")
            {
                // allowed
            }
            else if (role is "Investigator" or "Member")
            {
                if (existing.OwnerUserId != CurrentUserId()) throw new ForbiddenException();
            }
            else
            {
                throw new ForbiddenException();
            }
        }

        await _mediator.Send(new UpdateEventCommand(
            existing.Id, req.EventTypeId, req.WorkflowStatusId,
            req.Title, req.OccurredAt, req.Location, req.Description, req.OwnerUserId,
            req.RootCauseId, req.CorrectiveAction), ct);

        var (actorId, actorName) = ResolveActor();
        var hasAudit = false;

        if (existing.EventTypeId != req.EventTypeId)
        {
            var oldType = await _db.EventTypes.AsNoTracking().Where(t => t.Id == existing.EventTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? existing.EventTypeId.ToString();
            var newType = await _db.EventTypes.AsNoTracking().Where(t => t.Id == req.EventTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? req.EventTypeId.ToString();
            Audit.Record("event", existing.Id, existing.ClientId, "type_changed",
                $"Type changed from \"{oldType}\" to \"{newType}\".", actorId, actorName);
            hasAudit = true;
        }

        if (existing.WorkflowStatusId != req.WorkflowStatusId)
        {
            var oldStatus = await _db.WorkflowStatuses.AsNoTracking().Where(s => s.Id == existing.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? existing.WorkflowStatusId.ToString();
            var newStatus = await _db.WorkflowStatuses.AsNoTracking().Where(s => s.Id == req.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? req.WorkflowStatusId.ToString();
            Audit.Record("event", existing.Id, existing.ClientId, "status_changed",
                $"Status changed from \"{oldStatus}\" to \"{newStatus}\".", actorId, actorName);
            hasAudit = true;
        }

        if (existing.OwnerUserId != req.OwnerUserId)
        {
            string body;
            if (req.OwnerUserId is null)
            {
                body = "Owner unassigned.";
            }
            else
            {
                var ownerName = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == req.OwnerUserId.Value)
                    .Select(u => u.DisplayName)
                    .FirstOrDefaultAsync(ct) ?? req.OwnerUserId.Value.ToString();
                body = existing.OwnerUserId is null
                    ? $"Owner assigned to {ownerName}."
                    : $"Owner changed to {ownerName}.";
            }
            Audit.Record("event", existing.Id, existing.ClientId, "owner_changed", body, actorId, actorName);
            hasAudit = true;
        }

        if (hasAudit)
            await _db.SaveChangesAsync(ct);

        // Notifications (after save, non-blocking on failure)
        if (existing.WorkflowStatusId != req.WorkflowStatusId && existing.OwnerUserId.HasValue)
        {
            var newStatusForNotif = await _db.WorkflowStatuses.AsNoTracking()
                .Where(s => s.Id == req.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? req.WorkflowStatusId.ToString();
            await _notifications.NotifyStatusChangedAsync(existing.OwnerUserId.Value, actorId ?? 0, actorName,
                existing.ClientId, publicId, existing.Title, newStatusForNotif, ct);
        }

        if (existing.OwnerUserId != req.OwnerUserId && req.OwnerUserId.HasValue)
        {
            await _notifications.NotifyEventAssignedAsync(req.OwnerUserId.Value, actorId ?? 0, actorName,
                existing.ClientId, publicId, existing.Title, ct);
        }

        // Evaluate workflow rules
        {
            var updatedEvent = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == existing.Id, ct);
            if (updatedEvent is not null)
            {
                _ = _workflows.EvaluateAsync("event.updated", updatedEvent, existing, ct);

                if (existing.WorkflowStatusId != req.WorkflowStatusId)
                    _ = _workflows.EvaluateAsync("event.status_changed", updatedEvent, existing, ct);

                if (existing.OwnerUserId != req.OwnerUserId)
                    _ = _workflows.EvaluateAsync("event.assigned", updatedEvent, existing, ct);

                var isNowClosed = await _db.WorkflowStatuses.AsNoTracking()
                    .Where(s => s.Id == req.WorkflowStatusId).Select(s => s.IsClosed).FirstOrDefaultAsync(ct);
                var wasClosed = await _db.WorkflowStatuses.AsNoTracking()
                    .Where(s => s.Id == existing.WorkflowStatusId).Select(s => s.IsClosed).FirstOrDefaultAsync(ct);
                if (!wasClosed && isNowClosed)
                    _ = _workflows.EvaluateAsync("event.closed", updatedEvent, existing, ct);
            }
        }

        // Dispatch webhooks (fire-and-forget)
        {
            var newStatusIsClosed = await _db.WorkflowStatuses.AsNoTracking()
                .Where(s => s.Id == req.WorkflowStatusId).Select(s => s.IsClosed).FirstOrDefaultAsync(ct);
            var oldStatusIsClosed = await _db.WorkflowStatuses.AsNoTracking()
                .Where(s => s.Id == existing.WorkflowStatusId).Select(s => s.IsClosed).FirstOrDefaultAsync(ct);
            var newStatusName = await _db.WorkflowStatuses.AsNoTracking()
                .Where(s => s.Id == req.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct);
            var newTypeName = await _db.EventTypes.AsNoTracking()
                .Where(t => t.Id == req.EventTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct);
            var ownerName = req.OwnerUserId.HasValue
                ? await _db.Users.AsNoTracking().Where(u => u.Id == req.OwnerUserId.Value).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
                : null;

            var updatedSnapshot = new {
                PublicId = publicId, Title = req.Title, EventType = newTypeName,
                Status = newStatusName, StatusIsClosed = newStatusIsClosed,
                Location = req.Location, OccurredAt = req.OccurredAt,
                OwnerDisplayName = ownerName, CreatedAt = existing.CreatedAt,
            };

            _ = _webhooks.DispatchAsync(existing.ClientId, "event.updated",
                BuildWebhookPayload("event.updated", existing, newTypeName, newStatusName, newStatusIsClosed, ownerName));

            if (!oldStatusIsClosed && newStatusIsClosed)
                _ = _webhooks.DispatchAsync(existing.ClientId, "event.closed",
                    BuildWebhookPayload("event.closed", existing, newTypeName, newStatusName, newStatusIsClosed, ownerName));

            if (existing.OwnerUserId != req.OwnerUserId)
                _ = _webhooks.DispatchAsync(existing.ClientId, "event.assigned",
                    BuildWebhookPayload("event.assigned", existing, newTypeName, newStatusName, newStatusIsClosed, ownerName));
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("{publicId}/clone")]
    public async Task<ActionResult<object>> Clone(
        [FromRoute] string publicId, [FromQuery] long clientId = 0, CancellationToken ct = default)
    {
        var cloneQuery = _db.Events.AsNoTracking().Where(e => e.PublicId == publicId);
        if (clientId > 0) cloneQuery = cloneQuery.Where(e => e.ClientId == clientId);
        var existing = await cloneQuery.FirstOrDefaultAsync(ct);

        if (existing is null || !HasClientAccess(existing.ClientId)) throw new NotFoundException();
        if (!await IsManagerOrAboveAsync(_db, existing.ClientId, User, ct)) throw new ForbiddenException();

        var (actorId, actorName) = ResolveActor();

        var result = await _mediator.Send(new CreateEventCommand(
            existing.ClientId,
            existing.EventTypeId,
            existing.WorkflowStatusId,
            $"{existing.Title} (Copy)",
            existing.OccurredAt,
            existing.Location,
            existing.Description,
            actorId ?? existing.ReportedByUserId), ct);

        Audit.Record("event", result.EventId, existing.ClientId, "created",
            $"Duplicated from {existing.PublicId}.");
        await _db.SaveChangesAsync(ct);

        return Ok(new { publicId = result.PublicId });
    }

    [Authorize]
    [HttpPatch("bulk")]
    public async Task<IActionResult> BulkUpdate(
        [FromBody] BulkUpdateEventRequest req,
        CancellationToken ct = default)
    {
        if (req.ClientId == 0) throw new ValidationException("ClientId is required.");
        RequireClientAccess(req.ClientId);
        if (!await IsManagerOrAboveAsync(_db, req.ClientId, User, ct)) throw new ForbiddenException();
        if (req.EventIds is null || req.EventIds.Count == 0) throw new ValidationException("At least one EventId is required.");
        if (req.WorkflowStatusId is null && req.OwnerUserId is null && !req.ClearOwner)
            throw new ValidationException("At least one field to update must be specified.");

        var events = await _db.Events
            .Where(e => e.ClientId == req.ClientId && req.EventIds.Contains(e.Id))
            .ToListAsync(ct);

        if (events.Count == 0) return Ok(new { updated = 0 });

        var (actorId, actorName) = ResolveActor();
        var now = DateTimeOffset.UtcNow;

        string? newStatusName = null;
        if (req.WorkflowStatusId is not null)
            newStatusName = await _db.WorkflowStatuses.AsNoTracking()
                .Where(s => s.Id == req.WorkflowStatusId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct);

        string? ownerDisplayName = null;
        if (!req.ClearOwner && req.OwnerUserId is not null)
            ownerDisplayName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == req.OwnerUserId.Value)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(ct) ?? req.OwnerUserId.Value.ToString();

        foreach (var ev in events)
        {
            if (req.WorkflowStatusId is not null && ev.WorkflowStatusId != req.WorkflowStatusId.Value)
            {
                var oldStatusName = await _db.WorkflowStatuses.AsNoTracking()
                    .Where(s => s.Id == ev.WorkflowStatusId)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync(ct) ?? ev.WorkflowStatusId.ToString();
                Audit.Record("event", ev.Id, req.ClientId, "status_changed",
                    $"Status changed from \"{oldStatusName}\" to \"{newStatusName}\".", actorId, actorName);
                ev.WorkflowStatusId = req.WorkflowStatusId.Value;
            }

            if (req.ClearOwner && ev.OwnerUserId is not null)
            {
                Audit.Record("event", ev.Id, req.ClientId, "owner_changed", "Owner unassigned.", actorId, actorName);
                ev.OwnerUserId = null;
            }
            else if (!req.ClearOwner && req.OwnerUserId is not null && ev.OwnerUserId != req.OwnerUserId)
            {
                var body = ev.OwnerUserId is null
                    ? $"Owner assigned to {ownerDisplayName}."
                    : $"Owner changed to {ownerDisplayName}.";
                Audit.Record("event", ev.Id, req.ClientId, "owner_changed", body, actorId, actorName);
                ev.OwnerUserId = req.OwnerUserId;
            }

            ev.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        // Fan out notifications
        var notifTasks = new List<Task>();
        foreach (var ev in events)
        {
            if (req.WorkflowStatusId is not null && newStatusName is not null && ev.OwnerUserId.HasValue)
                notifTasks.Add(_notifications.NotifyStatusChangedAsync(ev.OwnerUserId.Value, actorId ?? 0, actorName,
                    req.ClientId, ev.PublicId, ev.Title, newStatusName, ct));

            if (!req.ClearOwner && req.OwnerUserId.HasValue && ev.OwnerUserId == req.OwnerUserId)
                notifTasks.Add(_notifications.NotifyEventAssignedAsync(req.OwnerUserId.Value, actorId ?? 0, actorName,
                    req.ClientId, ev.PublicId, ev.Title, ct));
        }
        if (notifTasks.Count > 0) await Task.WhenAll(notifTasks);

        return Ok(new { updated = events.Count });
    }

    [Authorize]
    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDelete(
        [FromBody] BulkDeleteEventRequest req,
        CancellationToken ct = default)
    {
        if (req.ClientId == 0) throw new ValidationException("ClientId is required.");
        RequireClientAccess(req.ClientId);
        if (!await IsAdminOfClientAsync(_db, req.ClientId, User, ct)) throw new ForbiddenException();
        if (req.EventPublicIds is null || req.EventPublicIds.Length == 0) throw new ValidationException("At least one event ID is required.");

        var events = await _db.Events
            .Where(e => e.ClientId == req.ClientId && req.EventPublicIds.Contains(e.PublicId))
            .ToListAsync(ct);

        if (events.Count == 0) return Ok(new { deleted = 0 });

        var now = DateTimeOffset.UtcNow;
        var deletedPublicIds = events.Select(e => e.PublicId).ToList();
        foreach (var ev in events)
            ev.DeletedAt = now;

        await _db.SaveChangesAsync(ct);

        foreach (var pid in deletedPublicIds)
            _ = _webhooks.DispatchAsync(req.ClientId, "event.deleted", new {
                @event = "event.deleted", timestamp = now, clientId = req.ClientId,
                data = new { publicId = pid },
            });

        return Ok(new { deleted = events.Count });
    }

    [Authorize]
    [HttpDelete("{publicId}")]
    public async Task<IActionResult> Delete(
        [FromRoute] string publicId,
        [FromQuery] long clientId = 0,
        CancellationToken ct = default)
    {
        var delQuery = _db.Events.Where(e => e.PublicId == publicId);
        if (clientId > 0) delQuery = delQuery.Where(e => e.ClientId == clientId);
        var ev = await delQuery.FirstOrDefaultAsync(ct);

        if (ev is null || !HasClientAccess(ev.ClientId)) throw new NotFoundException();
        if (!await IsAdminOfClientAsync(_db, ev.ClientId, User, ct)) throw new ForbiddenException();

        var evClientId = ev.ClientId;
        var pid        = ev.PublicId;
        ev.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _ = _webhooks.DispatchAsync(evClientId, "event.deleted", new {
            @event = "event.deleted", timestamp = DateTimeOffset.UtcNow, clientId = evClientId,
            data = new { publicId = pid },
        });

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object BuildWebhookPayload(
        string eventType, Event ev, string? typeName, string? statusName, bool statusIsClosed, string? ownerName)
        => new
        {
            @event    = eventType,
            timestamp = DateTimeOffset.UtcNow,
            clientId  = ev.ClientId,
            data = new
            {
                publicId           = ev.PublicId,
                title              = ev.Title,
                eventType          = typeName,
                status             = statusName,
                statusIsClosed,
                location           = ev.Location,
                occurredAt         = ev.OccurredAt,
                ownerDisplayName   = ownerName,
                createdAt          = ev.CreatedAt,
            },
        };

}
