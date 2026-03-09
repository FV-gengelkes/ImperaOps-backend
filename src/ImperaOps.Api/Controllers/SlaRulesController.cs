using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:long}/sla-rules")]
[Authorize]
public sealed class SlaRulesController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public SlaRulesController(ImperaOpsDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SlaRuleDto>>> GetAll(long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var eventTypes = await _db.EventTypes
            .AsNoTracking()
            .Where(t => t.ClientId == 0 || t.ClientId == clientId)
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var rules = await _db.SlaRules
            .AsNoTracking()
            .Where(r => r.ClientId == clientId)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        return Ok(rules.Select(r => new SlaRuleDto(
            r.Id,
            r.EventTypeId,
            r.EventTypeId.HasValue ? (eventTypes.TryGetValue(r.EventTypeId.Value, out var n) ? n : null) : null,
            r.Name,
            r.InvestigationHours,
            r.ClosureHours)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SlaRuleDto>> Create(
        long clientId, [FromBody] CreateSlaRuleRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");

        var rule = new SlaRule
        {
            ClientId           = clientId,
            EventTypeId        = req.EventTypeId,
            Name               = req.Name.Trim(),
            InvestigationHours = req.InvestigationHours,
            ClosureHours       = req.ClosureHours,
            CreatedAt          = DateTimeOffset.UtcNow,
        };

        _db.SlaRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return Ok(new SlaRuleDto(rule.Id, rule.EventTypeId, null, rule.Name, rule.InvestigationHours, rule.ClosureHours));
    }

    [HttpPut("{ruleId:long}")]
    public async Task<IActionResult> Update(
        long clientId, long ruleId, [FromBody] CreateSlaRuleRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");

        var rule = await _db.SlaRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ClientId == clientId, ct);
        if (rule is null) throw new NotFoundException();

        rule.EventTypeId        = req.EventTypeId;
        rule.Name               = req.Name.Trim();
        rule.InvestigationHours = req.InvestigationHours;
        rule.ClosureHours       = req.ClosureHours;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{ruleId:long}")]
    public async Task<IActionResult> Delete(long clientId, long ruleId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var rule = await _db.SlaRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ClientId == clientId, ct);
        if (rule is null) throw new NotFoundException();

        rule.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
