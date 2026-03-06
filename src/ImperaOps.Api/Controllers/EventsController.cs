using ImperaOps.Api.Contracts;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Commands;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Application.Events.Queries;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Notifications;
using ImperaOps.Infrastructure.Webhooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using SlaStatusDto = ImperaOps.Application.Events.Dtos.SlaStatusDto;

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

    public EventsController(IMediator mediator, ImperaOpsDbContext db, IEventReadRepository readRepo, INotificationService notifications, IWebhookDispatcher webhooks)
    {
        _mediator      = mediator;
        _db            = db;
        _readRepo      = readRepo;
        _notifications = notifications;
        _webhooks      = webhooks;
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
        CancellationToken ct = default)
    {
        if (clientId == 0) return BadRequest("clientId is required.");
        if (!HasClientAccess(clientId)) return NotFound();
        var res = await _mediator.Send(
            new GetEventListQuery(clientId, page, pageSize, eventTypeId, workflowStatusId, dateFrom, dateTo, search), ct);
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

        if (ids is null || ids.Count == 0) return BadRequest("At least one clientId is required.");
        ids = ids.Where(HasClientAccess).ToList();
        if (ids.Count == 0) return NotFound();
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
        if (clientId == 0) return BadRequest("clientId is required.");
        if (!HasClientAccess(clientId)) return NotFound();
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
    public async Task<ActionResult<IReadOnlyList<WorkloadRowDto>>> GetWorkload(
        [FromQuery] long clientId, CancellationToken ct = default)
    {
        if (clientId == 0) return BadRequest("clientId is required.");
        if (!HasClientAccess(clientId)) return NotFound();

        var closedStatusIds = await _db.WorkflowStatuses
            .Where(s => (s.ClientId == 0 || s.ClientId == clientId) && s.IsClosed)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var ownerGroups = await _db.Events
            .Where(e => e.ClientId == clientId && !closedStatusIds.Contains(e.WorkflowStatusId))
            .GroupBy(e => e.OwnerUserId)
            .Select(g => new { UserId = g.Key, OpenEvents = g.Count() })
            .ToListAsync(ct);

        var userIds = ownerGroups.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).ToList();

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var taskCounts = await _db.Tasks
            .Where(t => t.ClientId == clientId && !t.IsComplete && t.AssignedToUserId.HasValue)
            .GroupBy(t => t.AssignedToUserId!.Value)
            .Select(g => new { UserId = g.Key, OpenTasks = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.OpenTasks, ct);

        var result = ownerGroups
            .OrderByDescending(x => x.OpenEvents)
            .Select(x => new WorkloadRowDto(
                x.UserId,
                x.UserId.HasValue ? (names.GetValueOrDefault(x.UserId.Value) ?? "Unknown") : "Unassigned",
                x.OpenEvents,
                x.UserId.HasValue ? taskCounts.GetValueOrDefault(x.UserId.Value, 0) : 0))
            .ToList();

        return Ok(result);
    }

    [Authorize]
    [HttpGet("{publicId}")]
    public async Task<ActionResult<EventDetailDto>> GetDetail(
        [FromRoute] string publicId, CancellationToken ct = default)
    {
        var res = await _mediator.Send(new GetEventDetailQuery(publicId), ct);
        if (res is null || !HasClientAccess(res.ClientId)) return NotFound();

        // Compute SLA status
        var slaRule = await _db.SlaRules
            .AsNoTracking()
            .Where(r => r.ClientId == res.ClientId)
            .OrderBy(r => r.EventTypeId == null ? 1 : 0) // specific rule wins
            .FirstOrDefaultAsync(r =>
                r.EventTypeId == res.EventTypeId || r.EventTypeId == null, ct);

        if (slaRule != null)
        {
            var now = DateTimeOffset.UtcNow;
            var createdAt = new DateTimeOffset(res.CreatedAt, TimeSpan.Zero);

            DateTimeOffset? invDeadline = slaRule.InvestigationHours.HasValue
                ? createdAt.AddHours(slaRule.InvestigationHours.Value)
                : null;
            DateTimeOffset? closureDeadline = slaRule.ClosureHours.HasValue
                ? createdAt.AddHours(slaRule.ClosureHours.Value)
                : null;

            res.Sla = new SlaStatusDto
            {
                RuleId                = slaRule.Id,
                RuleName              = slaRule.Name,
                InvestigationDeadline = invDeadline,
                InvestigationBreached = invDeadline.HasValue && res.OwnerUserId == null && !res.WorkflowStatusIsClosed && now > invDeadline.Value,
                ClosureDeadline       = closureDeadline,
                ClosureBreached       = closureDeadline.HasValue && !res.WorkflowStatusIsClosed && now > closureDeadline.Value,
            };
        }

        return Ok(res);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CreateEventResponse>> Create(
        [FromBody] CreateEventRequest req, CancellationToken ct = default)
    {
        if (req.ClientId == 0)                            return BadRequest("ClientId is required.");
        if (!HasClientAccess(req.ClientId))               return NotFound();
        if (!await IsManagerOrAboveAsync(_db, req.ClientId, User, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Title))         return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.Location))      return BadRequest("Location is required.");
        if (string.IsNullOrWhiteSpace(req.Description))   return BadRequest("Description is required.");

        var result = await _mediator.Send(new CreateEventCommand(
            req.ClientId, req.EventTypeId, req.WorkflowStatusId,
            req.Title, req.OccurredAt, req.Location, req.Description,
            req.ReportedByUserId), ct);

        // Log audit event
        var (actorId, actorName) = ResolveActor();
        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = req.ClientId,
            EntityType      = "event",
            EntityId        = result.EventId,
            EventType       = "created",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = "Event reported.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });
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

        return Ok(new CreateEventResponse(result.EventId, result.PublicId));
    }

    [Authorize]
    [HttpPut("{publicId}")]
    public async Task<IActionResult> Update(
        [FromRoute] string publicId,
        [FromBody] UpdateEventRequest req,
        CancellationToken ct = default)
    {
        var existing = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PublicId == publicId, ct);

        if (existing is null || !HasClientAccess(existing.ClientId)) return NotFound();

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
                if (existing.OwnerUserId != CurrentUserId()) return Forbid();
            }
            else
            {
                return Forbid();
            }
        }

        await _mediator.Send(new UpdateEventCommand(
            existing.Id, req.EventTypeId, req.WorkflowStatusId,
            req.Title, req.OccurredAt, req.Location, req.Description, req.OwnerUserId,
            req.RootCauseId, req.CorrectiveAction), ct);

        var (actorId, actorName) = ResolveActor();
        var now = DateTimeOffset.UtcNow;
        var auditEvents = new List<AuditEvent>();

        if (existing.EventTypeId != req.EventTypeId)
        {
            var oldType = await _db.EventTypes.AsNoTracking().Where(t => t.Id == existing.EventTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? existing.EventTypeId.ToString();
            var newType = await _db.EventTypes.AsNoTracking().Where(t => t.Id == req.EventTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? req.EventTypeId.ToString();
            auditEvents.Add(NewAudit(existing.Id, existing.ClientId, "type_changed", actorId, actorName,
                $"Type changed from \"{oldType}\" to \"{newType}\".", now));
        }

        if (existing.WorkflowStatusId != req.WorkflowStatusId)
        {
            var oldStatus = await _db.WorkflowStatuses.AsNoTracking().Where(s => s.Id == existing.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? existing.WorkflowStatusId.ToString();
            var newStatus = await _db.WorkflowStatuses.AsNoTracking().Where(s => s.Id == req.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? req.WorkflowStatusId.ToString();
            auditEvents.Add(NewAudit(existing.Id, existing.ClientId, "status_changed", actorId, actorName,
                $"Status changed from \"{oldStatus}\" to \"{newStatus}\".", now));
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
            auditEvents.Add(NewAudit(existing.Id, existing.ClientId, "owner_changed", actorId, actorName, body, now));
        }

        if (auditEvents.Count > 0)
        {
            _db.AuditEvents.AddRange(auditEvents);
            await _db.SaveChangesAsync(ct);
        }

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
        [FromRoute] string publicId, CancellationToken ct = default)
    {
        var existing = await _db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PublicId == publicId, ct);

        if (existing is null || !HasClientAccess(existing.ClientId)) return NotFound();
        if (!await IsManagerOrAboveAsync(_db, existing.ClientId, User, ct)) return Forbid();

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

        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = existing.ClientId,
            EntityType      = "event",
            EntityId        = result.EventId,
            EventType       = "created",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = $"Duplicated from {existing.PublicId}.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new { publicId = result.PublicId });
    }

    [Authorize]
    [HttpPatch("bulk")]
    public async Task<IActionResult> BulkUpdate(
        [FromBody] BulkUpdateEventRequest req,
        CancellationToken ct = default)
    {
        if (req.ClientId == 0) return BadRequest("ClientId is required.");
        if (!HasClientAccess(req.ClientId)) return NotFound();
        if (!await IsManagerOrAboveAsync(_db, req.ClientId, User, ct)) return Forbid();
        if (req.EventIds is null || req.EventIds.Count == 0) return BadRequest("At least one EventId is required.");
        if (req.WorkflowStatusId is null && req.OwnerUserId is null && !req.ClearOwner)
            return BadRequest("At least one field to update must be specified.");

        var events = await _db.Events
            .Where(e => e.ClientId == req.ClientId && req.EventIds.Contains(e.Id))
            .ToListAsync(ct);

        if (events.Count == 0) return Ok(new { updated = 0 });

        var (actorId, actorName) = ResolveActor();
        var now = DateTimeOffset.UtcNow;
        var auditEvents = new List<AuditEvent>();

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
                auditEvents.Add(NewAudit(ev.Id, req.ClientId, "status_changed", actorId, actorName,
                    $"Status changed from \"{oldStatusName}\" to \"{newStatusName}\".", now));
                ev.WorkflowStatusId = req.WorkflowStatusId.Value;
            }

            if (req.ClearOwner && ev.OwnerUserId is not null)
            {
                auditEvents.Add(NewAudit(ev.Id, req.ClientId, "owner_changed", actorId, actorName, "Owner unassigned.", now));
                ev.OwnerUserId = null;
            }
            else if (!req.ClearOwner && req.OwnerUserId is not null && ev.OwnerUserId != req.OwnerUserId)
            {
                var body = ev.OwnerUserId is null
                    ? $"Owner assigned to {ownerDisplayName}."
                    : $"Owner changed to {ownerDisplayName}.";
                auditEvents.Add(NewAudit(ev.Id, req.ClientId, "owner_changed", actorId, actorName, body, now));
                ev.OwnerUserId = req.OwnerUserId;
            }

            ev.UpdatedAt = now;
        }

        if (auditEvents.Count > 0) _db.AuditEvents.AddRange(auditEvents);
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
        if (req.ClientId == 0) return BadRequest("ClientId is required.");
        if (!HasClientAccess(req.ClientId)) return Forbid();
        if (!await IsAdminOfClientAsync(_db, req.ClientId, User, ct)) return Forbid();
        if (req.EventPublicIds is null || req.EventPublicIds.Length == 0) return BadRequest("At least one event ID is required.");

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
        CancellationToken ct = default)
    {
        var ev = await _db.Events
            .FirstOrDefaultAsync(e => e.PublicId == publicId, ct);

        if (ev is null || !HasClientAccess(ev.ClientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, ev.ClientId, User, ct)) return Forbid();

        var clientId = ev.ClientId;
        var pid      = ev.PublicId;
        ev.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _ = _webhooks.DispatchAsync(clientId, "event.deleted", new {
            @event = "event.deleted", timestamp = DateTimeOffset.UtcNow, clientId,
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

    private static AuditEvent NewAudit(
        long entityId, long clientId, string eventType,
        long? userId, string userDisplayName, string body, DateTimeOffset createdAt)
        => new()
        {
            ClientId        = clientId,
            EntityType      = "event",
            EntityId        = entityId,
            EventType       = eventType,
            UserId          = userId,
            UserDisplayName = userDisplayName,
            Body            = body,
            CreatedAt       = createdAt,
        };
}
