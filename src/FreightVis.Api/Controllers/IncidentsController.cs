using FreightVis.Api.Contracts;
using FreightVis.Application.Abstractions;
using FreightVis.Application.Incidents.Commands;
using FreightVis.Application.Incidents.Dtos;
using FreightVis.Application.Incidents.Queries;
using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly FreightVisDbContext _db;
    private readonly IIncidentReadRepository _readRepo;

    public IncidentsController(IMediator mediator, FreightVisDbContext db, IIncidentReadRepository readRepo)
    {
        _mediator = mediator;
        _db = db;
        _readRepo = readRepo;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<IncidentListItemDto>>> GetList(
        [FromQuery] Guid clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] int? type = null,
        [FromQuery] int? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (clientId == Guid.Empty) return BadRequest("clientId is required.");
        var res = await _mediator.Send(new GetIncidentListQuery(clientId, page, pageSize, type, status, dateFrom, dateTo, Search: search), ct);
        return Ok(res);
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<IncidentAnalyticsDto>> GetAnalytics(
        [FromQuery] List<Guid>? clientIds,
        [FromQuery] Guid clientId = default,
        CancellationToken ct = default)
    {
        // Support both ?clientIds=id1&clientIds=id2 (new) and ?clientId=id (legacy)
        var ids = (clientIds?.Where(id => id != Guid.Empty).ToList() is { Count: > 0 } list)
            ? list
            : clientId != Guid.Empty ? new List<Guid> { clientId } : null;

        if (ids is null || ids.Count == 0) return BadRequest("At least one clientId is required.");
        var res = await _mediator.Send(new GetIncidentAnalyticsQuery(ids), ct);
        return Ok(res);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid clientId,
        [FromQuery] int? type, [FromQuery] int? status,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        if (clientId == Guid.Empty) return BadRequest("clientId is required.");
        var rows = await _readRepo.GetExportDataAsync(clientId, type, status, dateFrom, dateTo, search, ct);
        var csv  = BuildCsv(rows);
        var filename = $"incidents-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", filename);
    }

    private static string BuildCsv(IReadOnlyList<IncidentExportRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ref #,Occurred At,Type,Status,Location,Description,Owner");
        foreach (var r in rows)
            sb.AppendLine(string.Join(",",
                $"INC-{r.ReferenceNumber:D4}",
                CsvEscape(r.OccurredAt.ToString("yyyy-MM-dd HH:mm")),
                CsvEscape(r.Type),
                CsvEscape(r.Status),
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

    [HttpGet("ref/{refNumber:int}")]
    public async Task<ActionResult<IncidentDetailDto>> GetByRef(
        [FromRoute] int refNumber,
        [FromQuery] Guid clientId,
        CancellationToken ct = default)
    {
        if (clientId == Guid.Empty) return BadRequest("clientId is required.");
        var i = await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ClientId == clientId && i.ReferenceNumber == refNumber, ct);
        if (i is null) return NotFound();
        var reporterName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == i.ReportedByUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);
        return Ok(new IncidentDetailDto(
            i.Id, i.ClientId, i.Type, i.Status, i.OccurredAt,
            i.Location, i.Description, i.ReportedByUserId, i.OwnerUserId,
            i.CreatedAt, i.UpdatedAt, i.ReferenceNumber, reporterName));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IncidentDetailDto>> GetDetail([FromRoute] Guid id, CancellationToken ct = default)
    {
        var res = await _mediator.Send(new GetIncidentDetailQuery(id), ct);
        return res is null ? NotFound() : Ok(res);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CreateIncidentResponse>> Create(
        [FromBody] CreateIncidentRequest req,
        CancellationToken ct = default)
    {
        if (req.ClientId == Guid.Empty) return BadRequest("ClientId is required.");
        if (string.IsNullOrWhiteSpace(req.Location)) return BadRequest("Location is required.");
        if (string.IsNullOrWhiteSpace(req.Description)) return BadRequest("Description is required.");

        var nextRef = (await _db.Incidents
            .Where(i => i.ClientId == req.ClientId)
            .MaxAsync(i => (int?)i.ReferenceNumber, ct) ?? 0) + 1;

        var result = await _mediator.Send(new CreateIncidentCommand(
            req.ClientId,
            req.Type,
            req.OccurredAt,
            req.Location,
            req.Description,
            req.ReportedByUserId,
            nextRef
        ), ct);

        // Log "Incident reported" activity event
        var (actorId, actorName) = ResolveActor();

        _db.IncidentEvents.Add(new IncidentEvent
        {
            Id              = Guid.NewGuid(),
            IncidentId      = result.IncidentId,
            ClientId        = req.ClientId,
            EventType       = "incident_created",
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = "Incident reported.",
            CreatedAt       = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new CreateIncidentResponse(result.IncidentId, nextRef));
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateIncidentRequest req,
        CancellationToken ct = default)
    {
        // Snapshot the current state before the update so we can diff it
        var existing = await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (existing is null) return NotFound();

        await _mediator.Send(new UpdateIncidentCommand(
            id,
            req.Type,
            req.Status,
            req.OccurredAt,
            req.Location,
            req.Description,
            req.OwnerUserId
        ), ct);

        // Build activity events for changed fields
        var (actorId, actorName) = ResolveActor();
        var now             = DateTimeOffset.UtcNow;
        var events          = new List<IncidentEvent>();

        if (existing.Type != req.Type)
        {
            var labels   = await GetLookupLabelsAsync(existing.ClientId, "incident_type", ct);
            var oldLabel = labels.GetValueOrDefault(existing.Type, existing.Type.ToString());
            var newLabel = labels.GetValueOrDefault(req.Type, req.Type.ToString());
            events.Add(NewEvent(id, existing.ClientId, "type_changed", actorId, actorName,
                $"Type changed from \"{oldLabel}\" to \"{newLabel}\".", now));
        }

        if (existing.Status != req.Status)
        {
            var labels   = await GetLookupLabelsAsync(existing.ClientId, "status", ct);
            var oldLabel = labels.GetValueOrDefault(existing.Status, existing.Status.ToString());
            var newLabel = labels.GetValueOrDefault(req.Status, req.Status.ToString());
            events.Add(NewEvent(id, existing.ClientId, "status_changed", actorId, actorName,
                $"Status changed from \"{oldLabel}\" to \"{newLabel}\".", now));
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
                var ownerName = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == req.OwnerUserId.Value)
                    .Select(u => u.DisplayName)
                    .FirstOrDefaultAsync(ct) ?? req.OwnerUserId.Value.ToString();

                body = existing.OwnerUserId is null
                    ? $"Owner assigned to {ownerName}."
                    : $"Owner changed to {ownerName}.";
            }
            events.Add(NewEvent(id, existing.ClientId, "owner_changed", actorId, actorName, body, now));
        }

        if (events.Count > 0)
        {
            _db.IncidentEvents.AddRange(events);
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    [Authorize]
    [HttpPatch("bulk")]
    public async Task<IActionResult> BulkUpdate(
        [FromBody] BulkUpdateIncidentRequest req,
        CancellationToken ct = default)
    {
        if (req.ClientId == Guid.Empty)           return BadRequest("ClientId is required.");
        if (req.IncidentIds is null || req.IncidentIds.Count == 0) return BadRequest("At least one IncidentId is required.");
        if (req.Status is null && req.OwnerUserId is null && !req.ClearOwner)
            return BadRequest("At least one field to update must be specified.");

        var incidents = await _db.Incidents
            .Where(i => i.ClientId == req.ClientId && req.IncidentIds.Contains(i.Id))
            .ToListAsync(ct);

        if (incidents.Count == 0) return Ok(new { updated = 0 });

        var (actorId, actorName) = ResolveActor();
        var now = DateTimeOffset.UtcNow;
        var events = new List<IncidentEvent>();

        // Resolve labels once, outside the loop
        Dictionary<int, string>? statusLabels = null;
        if (req.Status is not null)
            statusLabels = await GetLookupLabelsAsync(req.ClientId, "status", ct);

        string? ownerDisplayName = null;
        if (!req.ClearOwner && req.OwnerUserId is not null)
            ownerDisplayName = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == req.OwnerUserId.Value)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(ct) ?? req.OwnerUserId.Value.ToString();

        foreach (var incident in incidents)
        {
            if (req.Status is not null && incident.Status != req.Status.Value)
            {
                var oldLabel = statusLabels!.GetValueOrDefault(incident.Status, incident.Status.ToString());
                var newLabel = statusLabels!.GetValueOrDefault(req.Status.Value, req.Status.Value.ToString());
                events.Add(NewEvent(incident.Id, req.ClientId, "status_changed", actorId, actorName,
                    $"Status changed from \"{oldLabel}\" to \"{newLabel}\".", now));
                incident.Status = req.Status.Value;
            }

            if (req.ClearOwner && incident.OwnerUserId is not null)
            {
                events.Add(NewEvent(incident.Id, req.ClientId, "owner_changed", actorId, actorName,
                    "Owner unassigned.", now));
                incident.OwnerUserId = null;
            }
            else if (!req.ClearOwner && req.OwnerUserId is not null && incident.OwnerUserId != req.OwnerUserId)
            {
                var body = incident.OwnerUserId is null
                    ? $"Owner assigned to {ownerDisplayName}."
                    : $"Owner changed to {ownerDisplayName}.";
                events.Add(NewEvent(incident.Id, req.ClientId, "owner_changed", actorId, actorName, body, now));
                incident.OwnerUserId = req.OwnerUserId;
            }

            incident.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (events.Count > 0)
            _db.IncidentEvents.AddRange(events);

        await _db.SaveChangesAsync(ct);
        return Ok(new { updated = incidents.Count });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the authenticated user's ID (null if unauthenticated) and the display name
    /// to record in activity events. Super-admins are shown as "FreightVis Admin".
    /// </summary>
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

    private static IncidentEvent NewEvent(
        Guid incidentId, Guid clientId, string eventType,
        Guid? userId, string userDisplayName, string body, DateTimeOffset createdAt)
        => new()
        {
            Id              = Guid.NewGuid(),
            IncidentId      = incidentId,
            ClientId        = clientId,
            EventType       = eventType,
            UserId          = userId,
            UserDisplayName = userDisplayName,
            Body            = body,
            CreatedAt       = createdAt,
        };

    private async Task<Dictionary<int, string>> GetLookupLabelsAsync(
        Guid clientId, string fieldKey, CancellationToken ct)
    {
        var rows = await _db.IncidentLookups
            .AsNoTracking()
            .Where(l => (l.ClientId == Guid.Empty || l.ClientId == clientId)
                        && l.FieldKey == fieldKey
                        && l.IsActive)
            .ToListAsync(ct);

        // Client rows take priority over system rows for the same Value
        var dict = new Dictionary<int, string>();
        foreach (var r in rows.OrderBy(r => r.IsSystem ? 0 : 1))
            dict[r.Value] = r.Label;

        return dict;
    }
}
