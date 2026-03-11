using System.Text.Json;
using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:long}/workflow-rules")]
[Authorize]
public sealed class WorkflowRulesController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    private static readonly HashSet<string> ValidTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "event.created", "event.updated", "event.status_changed",
        "event.assigned", "event.closed",
    };

    public WorkflowRulesController(ImperaOpsDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowRuleDto>>> GetAll(
        long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var rules = await _db.WorkflowRules.AsNoTracking()
            .Where(r => r.ClientId == clientId)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var userIds = rules.Where(r => r.CreatedByUserId.HasValue)
            .Select(r => r.CreatedByUserId!.Value).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        // Get execution counts per rule
        var ruleIds = rules.Select(r => r.Id).ToList();
        var execCounts = await _db.WorkflowRuleExecutions
            .Where(e => ruleIds.Contains(e.WorkflowRuleId))
            .GroupBy(e => e.WorkflowRuleId)
            .Select(g => new { RuleId = g.Key, Total = g.Count(), Failed = g.Count(e => !e.Success) })
            .ToDictionaryAsync(x => x.RuleId, ct);

        return Ok(rules.Select(r =>
        {
            execCounts.TryGetValue(r.Id, out var counts);
            return new WorkflowRuleDto(
                r.Id, r.ClientId, r.Name, r.Description, r.TriggerType,
                r.IsActive, r.SortOrder, r.StopOnMatch,
                JsonDocument.Parse(r.ConditionsJson).RootElement,
                JsonDocument.Parse(r.ActionsJson).RootElement,
                r.CreatedByUserId,
                r.CreatedByUserId.HasValue ? userNames.GetValueOrDefault(r.CreatedByUserId.Value) : null,
                r.CreatedAt, r.UpdatedAt,
                counts?.Total ?? 0, counts?.Failed ?? 0);
        }).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<WorkflowRuleDto>> GetById(
        long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var r = await _db.WorkflowRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId, ct);
        if (r is null) throw new NotFoundException();

        var creatorName = r.CreatedByUserId.HasValue
            ? await _db.Users.AsNoTracking().Where(u => u.Id == r.CreatedByUserId.Value).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
            : null;

        var counts = await _db.WorkflowRuleExecutions
            .Where(e => e.WorkflowRuleId == id)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Failed = g.Count(e => !e.Success) })
            .FirstOrDefaultAsync(ct);

        return Ok(new WorkflowRuleDto(
            r.Id, r.ClientId, r.Name, r.Description, r.TriggerType,
            r.IsActive, r.SortOrder, r.StopOnMatch,
            JsonDocument.Parse(r.ConditionsJson).RootElement,
            JsonDocument.Parse(r.ActionsJson).RootElement,
            r.CreatedByUserId, creatorName, r.CreatedAt, r.UpdatedAt,
            counts?.Total ?? 0, counts?.Failed ?? 0));
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowRuleDto>> Create(
        long clientId, [FromBody] CreateWorkflowRuleRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        Validate(req.Name, req.TriggerType);

        var maxSort = await _db.WorkflowRules
            .Where(r => r.ClientId == clientId)
            .MaxAsync(r => (int?)r.SortOrder, ct) ?? -1;

        var now = DateTimeOffset.UtcNow;
        var rule = new WorkflowRule
        {
            ClientId = clientId,
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            TriggerType = req.TriggerType,
            IsActive = req.IsActive,
            SortOrder = maxSort + 1,
            StopOnMatch = req.StopOnMatch,
            ConditionsJson = req.Conditions.GetRawText(),
            ActionsJson = req.Actions.GetRawText(),
            CreatedByUserId = CurrentUserId(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.WorkflowRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        var creatorName = await _db.Users.AsNoTracking()
            .Where(u => u.Id == rule.CreatedByUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);

        return Ok(new WorkflowRuleDto(
            rule.Id, rule.ClientId, rule.Name, rule.Description, rule.TriggerType,
            rule.IsActive, rule.SortOrder, rule.StopOnMatch,
            JsonDocument.Parse(rule.ConditionsJson).RootElement,
            JsonDocument.Parse(rule.ActionsJson).RootElement,
            rule.CreatedByUserId, creatorName, rule.CreatedAt, rule.UpdatedAt, 0, 0));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long clientId, long id, [FromBody] UpdateWorkflowRuleRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var rule = await _db.WorkflowRules.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId, ct);
        if (rule is null) throw new NotFoundException();

        Validate(req.Name, req.TriggerType);

        rule.Name = req.Name.Trim();
        rule.Description = req.Description?.Trim();
        rule.TriggerType = req.TriggerType;
        rule.IsActive = req.IsActive;
        rule.StopOnMatch = req.StopOnMatch;
        rule.ConditionsJson = req.Conditions.GetRawText();
        rule.ActionsJson = req.Actions.GetRawText();
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(
        long clientId, [FromBody] ReorderWorkflowRulesRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var rules = await _db.WorkflowRules
            .Where(r => r.ClientId == clientId)
            .ToListAsync(ct);

        int order = 0;
        foreach (var id in req.OrderedIds)
        {
            var rule = rules.FirstOrDefault(r => r.Id == id);
            if (rule is not null) rule.SortOrder = order++;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:long}/toggle")]
    public async Task<IActionResult> Toggle(
        long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var rule = await _db.WorkflowRules.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId, ct);
        if (rule is null) throw new NotFoundException();

        rule.IsActive = !rule.IsActive;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { isActive = rule.IsActive });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var rule = await _db.WorkflowRules.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId, ct);
        if (rule is null) throw new NotFoundException();

        rule.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Execution History ──────────────────────────────────────────────

    [HttpGet("executions")]
    public async Task<ActionResult<IReadOnlyList<WorkflowRuleExecutionDto>>> GetExecutions(
        long clientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var execs = await _db.WorkflowRuleExecutions
            .Where(e => e.ClientId == clientId)
            .OrderByDescending(e => e.ExecutedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ruleIds = execs.Select(e => e.WorkflowRuleId).Distinct().ToList();
        var ruleNames = await _db.WorkflowRules.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => ruleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name })
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var eventIds = execs.Select(e => e.EventId).Distinct().ToList();
        var eventPids = await _db.Events.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new { e.Id, e.PublicId })
            .ToDictionaryAsync(e => e.Id, e => e.PublicId, ct);

        return Ok(execs.Select(e => new WorkflowRuleExecutionDto(
            e.Id, e.WorkflowRuleId, ruleNames.GetValueOrDefault(e.WorkflowRuleId),
            e.EventId, eventPids.GetValueOrDefault(e.EventId),
            e.TriggerType, e.ActionsExecuted, e.Success, e.ErrorMessage, e.ExecutedAt
        )).ToList());
    }

    // ── Validation ─────────────────────────────────────────────────────

    private static void Validate(string name, string triggerType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Name is required.");
        if (!ValidTriggers.Contains(triggerType))
            throw new ValidationException($"Invalid trigger type. Must be one of: {string.Join(", ", ValidTriggers)}");
    }
}
