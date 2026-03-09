using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Ai;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public sealed class AiController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly IClaudeService _ai;

    public AiController(ImperaOpsDbContext db, IClaudeService ai)
    {
        _db = db;
        _ai = ai;
    }

    [HttpPost("categorize")]
    public async Task<ActionResult<AiCategorizeResponse>> Categorize(
        [FromBody] AiCategorizeRequest req, CancellationToken ct)
    {
        RequireClientAccess(req.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, req.ClientId, User, ct)) throw new ForbiddenException();

        var eventTypes = await _db.EventTypes.AsNoTracking()
            .Where(t => t.ClientId == req.ClientId && t.IsActive)
            .Select(t => new NamedItem(t.Id, t.Name))
            .ToListAsync(ct);

        var rootCauses = await _db.RootCauseTaxonomyItems.AsNoTracking()
            .Where(r => r.ClientId == req.ClientId)
            .Select(r => new NamedItem(r.Id, r.Name))
            .ToListAsync(ct);

        var result = await _ai.CategorizeAsync(req.Title, req.Description, eventTypes, rootCauses, ct);

        return Ok(new AiCategorizeResponse(
            result.SuggestedEventTypeId,
            result.SuggestedEventTypeName,
            result.EventTypeConfidence,
            result.SuggestedRootCauseId,
            result.SuggestedRootCauseName,
            result.RootCauseConfidence,
            result.Reasoning));
    }

    [HttpPost("investigate")]
    public async Task<ActionResult<AiInvestigateResponse>> Investigate(
        [FromBody] AiInvestigateRequest req, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == req.PublicId, ct);
        if (ev is null) throw new NotFoundException();
        RequireClientAccess(ev.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, ev.ClientId, User, ct)) throw new ForbiddenException();

        var inv = await _db.Investigations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);

        var witnesses = await _db.InvestigationWitnesses.AsNoTracking()
            .Where(w => inv != null && w.InvestigationId == inv.Id)
            .Select(w => w.Statement)
            .ToListAsync(ct);

        var evidence = await _db.InvestigationEvidence.AsNoTracking()
            .Where(e => inv != null && e.InvestigationId == inv.Id)
            .Select(e => $"{e.Title}: {e.Description ?? ""}")
            .ToListAsync(ct);

        var result = await _ai.SuggestInvestigationAsync(
            ev.Title, ev.Description, ev.Location,
            inv?.Summary,
            witnesses, evidence, ct);

        return Ok(new AiInvestigateResponse(
            result.SuggestedRootCause,
            result.SuggestedCorrectiveActions,
            result.Reasoning));
    }

    [HttpPost("analyze-trends")]
    public async Task<ActionResult<AiTrendAnalysisResponse>> AnalyzeTrends(
        [FromBody] AiTrendAnalysisRequest req, CancellationToken ct)
    {
        RequireClientAccess(req.ClientId);
        if (!await IsInvestigatorOrAboveAsync(_db, req.ClientId, User, ct)) throw new ForbiddenException();

        var alerts = await _db.InsightAlerts.AsNoTracking()
            .Where(a => a.ClientId == req.ClientId)
            .OrderByDescending(a => a.GeneratedAt)
            .Take(50)
            .Select(a => new AlertInfo(a.AlertType, a.Severity, a.Title, a.Body))
            .ToListAsync(ct);

        if (alerts.Count == 0)
            return Ok(new AiTrendAnalysisResponse("No insight alerts available to analyze."));

        var summary = await _ai.AnalyzeTrendsAsync(alerts, ct);
        return Ok(new AiTrendAnalysisResponse(summary));
    }
}
