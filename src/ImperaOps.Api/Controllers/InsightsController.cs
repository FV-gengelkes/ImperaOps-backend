using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/insights")]
[Authorize]
public sealed class InsightsController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public InsightsController(ImperaOpsDbContext db) => _db = db;

    // GET api/v1/insights?clientId=X
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var alerts = await _db.InsightAlerts
            .AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.GeneratedAt)
            .Take(100)
            .ToListAsync(ct);

        return Ok(alerts.Select(a => new InsightAlertDto(
            a.Id, a.ClientId, a.AlertType, a.Severity, a.Title, a.Body,
            a.MetadataJson, a.RelatedEventIds, a.IsAcknowledged,
            a.AcknowledgedAt?.ToString("o"), a.GeneratedAt.ToString("o"),
            a.AiSummary)));
    }

    // GET api/v1/insights/summary?clientId=X
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var unacked = await _db.InsightAlerts
            .AsNoTracking()
            .Where(a => a.ClientId == clientId && !a.IsAcknowledged)
            .ToListAsync(ct);

        var recent = unacked
            .OrderByDescending(a => a.GeneratedAt)
            .Take(3)
            .Select(a => new InsightAlertDto(
                a.Id, a.ClientId, a.AlertType, a.Severity, a.Title, a.Body,
                a.MetadataJson, a.RelatedEventIds, a.IsAcknowledged,
                a.AcknowledgedAt?.ToString("o"), a.GeneratedAt.ToString("o")))
            .ToList();

        return Ok(new InsightSummaryDto(
            unacked.Count,
            unacked.Count(a => a.Severity == "critical"),
            unacked.Count(a => a.Severity == "warning"),
            unacked.Count(a => a.Severity == "info"),
            recent));
    }

    // PATCH api/v1/insights/{id}/acknowledge
    [HttpPatch("{id:long}/acknowledge")]
    public async Task<IActionResult> Acknowledge(long id, CancellationToken ct)
    {
        var alert = await _db.InsightAlerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert == null) throw new NotFoundException();
        RequireClientAccess(alert.ClientId);
        if (!await IsManagerOrAboveAsync(_db, alert.ClientId, User, ct)) throw new ForbiddenException();

        alert.IsAcknowledged = true;
        alert.AcknowledgedByUserId = CurrentUserId();
        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
