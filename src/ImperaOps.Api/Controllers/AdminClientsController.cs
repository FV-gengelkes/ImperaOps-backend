using ImperaOps.Api.Contracts;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "SuperAdmin")]
public sealed class AdminClientsController(
    ImperaOpsDbContext db,
    IClientAdminService clientAdmin) : ScopedControllerBase
{
    // ── Clients ──────────────────────────────────────────────────────────

    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<AdminClientDto>>> GetClients(CancellationToken ct)
    {
        var clients = await db.Clients.AsNoTracking().ToListAsync(ct);

        var userCounts = await db.UserClientAccess
            .AsNoTracking()
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new { a, u })
            .Where(x => !x.u.IsSuperAdmin)
            .GroupBy(x => x.a.ClientId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var parentNames = clients.ToDictionary(c => c.Id, c => c.Name);

        var result = clients
            .OrderBy(c => c.Name)
            .Select(c => new AdminClientDto(
                c.Id, c.Name, c.Slug, c.ParentClientId,
                c.ParentClientId.HasValue && parentNames.TryGetValue(c.ParentClientId.Value, out var pn) ? pn : null,
                c.Status,
                userCounts.TryGetValue(c.Id, out var cnt) ? cnt : 0,
                c.CreatedAt,
                ParseTemplateIds(c.AppliedTemplateIds)))
            .ToList();

        return Ok(result);
    }

    [HttpPost("clients")]
    public async Task<ActionResult<AdminClientDto>> CreateClient(
        [FromBody] CreateClientRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Name is required.");

        if (req.ParentClientId.HasValue)
        {
            var parent = await db.Clients.FindAsync([req.ParentClientId.Value], ct);
            if (parent is null) throw new ValidationException("Parent client not found.");
        }

        string? templateName = null;
        if (!string.IsNullOrWhiteSpace(req.TemplateId))
        {
            if (!TemplateLibrary.All.TryGetValue(req.TemplateId, out var tpl))
                throw new ValidationException("Template not found.");
            templateName = tpl.Name;
        }

        var client = new Client
        {
            Name           = req.Name.Trim(),
            Slug           = GenerateSlug(req.Name),
            ParentClientId = req.ParentClientId,
            Status         = req.Status ?? "Active",
            CreatedAt      = DateTimeOffset.UtcNow,
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);

        var auditBody = templateName is not null
            ? $"Client \"{client.Name}\" created with template \"{templateName}\"."
            : $"Client \"{client.Name}\" created.";
        Audit.Record("client", client.Id, client.Id, "created", auditBody);
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(req.TemplateId))
        {
            await clientAdmin.ApplyTemplateAsync(client.Id, req.TemplateId, ct,
                seedDemoData: req.SeedDemoData, ownerUserId: CurrentUserId());
            client.AppliedTemplateIds = JsonSerializer.Serialize(new[] { req.TemplateId });
            await db.SaveChangesAsync(ct);
        }

        return CreatedAtAction(nameof(GetClients), new AdminClientDto(
            client.Id, client.Name, client.Slug, client.ParentClientId, null,
            client.Status, 0, client.CreatedAt,
            ParseTemplateIds(client.AppliedTemplateIds)));
    }

    [HttpPut("clients/{id:long}")]
    public async Task<IActionResult> UpdateClient(
        long id, [FromBody] UpdateClientRequest req, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");
        if (req.ParentClientId == id) throw new ValidationException("A client cannot be its own parent.");

        var oldName   = client.Name;
        var oldStatus = client.Status;
        var oldParent = client.ParentClientId;

        client.Name           = req.Name.Trim();
        client.ParentClientId = req.ParentClientId;
        client.Status         = req.Status;

        var changes = new List<string>();
        if (oldName   != client.Name)           changes.Add($"name → \"{client.Name}\"");
        if (oldStatus != client.Status)         changes.Add($"status → \"{client.Status}\"");
        if (oldParent != client.ParentClientId) changes.Add("parent changed");

        Audit.Record("client", id, id, "updated",
            changes.Count > 0 ? string.Join("; ", changes) + "." : "Updated.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("clients/{id:long}/status")]
    public async Task<IActionResult> UpdateClientStatus(
        long id, [FromBody] UpdateClientStatusRequest req, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        var allowed = new[] { "Active", "Inactive", "Demo", "SalesDemo" };
        if (!allowed.Contains(req.Status))
            throw new ValidationException($"Invalid status. Allowed: {string.Join(", ", allowed)}.");

        var old = client.Status;
        client.Status = req.Status;
        Audit.Record("client", id, id, "status_changed",
            $"Status changed from \"{old}\" to \"{client.Status}\".");
        await db.SaveChangesAsync(ct);
        return Ok(new { client.Id, client.Status });
    }

    // ── Event Templates ──────────────────────────────────────────────────

    [HttpGet("/api/v1/event-templates")]
    public IActionResult GetEventTemplates()
    {
        var result = TemplateLibrary.All.Values
            .OrderBy(t => t.Name)
            .Select(t => new EventTemplateListItemDto(
                t.Id, t.Name, t.Description, t.Industry,
                t.EventTypes.Count, t.WorkflowStatuses.Count, t.CustomFields.Count))
            .ToList();
        return Ok(result);
    }

    [HttpPost("clients/{id:long}/apply-template/{templateId}")]
    public async Task<IActionResult> ApplyTemplate(
        long id, string templateId, [FromQuery] bool seedDemoData, CancellationToken ct)
    {
        if (!TemplateLibrary.All.TryGetValue(templateId, out var template))
            throw new NotFoundException("Template not found.");

        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException("Client not found.");

        var applied = ParseTemplateIds(client.AppliedTemplateIds);
        if (applied.Contains(templateId))
            throw new ConflictException($"Template \"{template.Name}\" has already been applied to this client.");

        await clientAdmin.ApplyTemplateAsync(id, templateId, ct,
            seedDemoData: seedDemoData, ownerUserId: CurrentUserId());

        var updated = applied.Append(templateId).ToList();
        client.AppliedTemplateIds = JsonSerializer.Serialize(updated);

        Audit.Record("client", id, id, "template_applied",
            $"Template \"{template.Name}\" applied.");
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── Purge & Reset ────────────────────────────────────────────────────

    [HttpPost("clients/{id:long}/purge-events")]
    public async Task<IActionResult> PurgeEventData(
        long id, [FromBody] PurgeConfirmRequest req, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        if (!string.Equals(req.ConfirmName, client.Name, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Client name does not match. Type the exact client name to confirm.");

        var purgedCount = await clientAdmin.PurgeEventDataAsync(id, ct);

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM InsightAlerts WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM Notifications WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM ClientCounters WHERE ClientId = {0} AND CounterName = 'event'", [id], ct);

        Audit.Record("client", id, id, "events_purged",
            $"Purged {purgedCount} events and all dependent records.");
        await db.SaveChangesAsync(ct);

        return Ok(new { purgedEventCount = purgedCount });
    }

    [HttpPost("clients/{id:long}/reset")]
    public async Task<IActionResult> ResetClient(
        long id, [FromBody] ResetClientRequest req, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        if (!string.Equals(req.ConfirmName, client.Name, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Client name does not match. Type the exact client name to confirm.");

        var purgedCount = await clientAdmin.PurgeEventDataAsync(id, ct);

        await db.Database.ExecuteSqlRawAsync("DELETE FROM InsightAlerts WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Notifications WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ClientCounters WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM SlaRules WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CustomFieldValues WHERE CustomFieldId IN (SELECT Id FROM CustomFields WHERE ClientId = {0})", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CustomFields WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM WorkflowTransitions WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM WorkflowStatuses WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM EventTypes WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM RootCauseTaxonomyItems WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ClientWebhooks WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM DocumentReferences WHERE ClientId = {0}", [id], ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ClientDocuments WHERE ClientId = {0}", [id], ct);

        client.AppliedTemplateIds = null;

        Audit.Record("client", id, id, "client_reset",
            $"Full client reset. Purged {purgedCount} events and all configuration.");
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(req.TemplateId))
        {
            if (!TemplateLibrary.All.TryGetValue(req.TemplateId, out var template))
                throw new ValidationException("Template not found.");

            await clientAdmin.ApplyTemplateAsync(id, req.TemplateId, ct,
                seedDemoData: req.SeedDemoData, ownerUserId: CurrentUserId());
            client.AppliedTemplateIds = JsonSerializer.Serialize(new[] { req.TemplateId });
            Audit.Record("client", id, id, "template_applied",
                $"Template \"{template.Name}\" re-applied after reset.");
            await db.SaveChangesAsync(ct);
        }

        return Ok(new { purgedEventCount = purgedCount, templateReapplied = !string.IsNullOrWhiteSpace(req.TemplateId) });
    }

    // ── SLA Rules ────────────────────────────────────────────────────────

    [HttpGet("clients/{id:long}/sla-rules")]
    public async Task<ActionResult<IReadOnlyList<SlaRuleDto>>> GetSlaRules(long id, CancellationToken ct)
    {
        var eventTypes = await db.EventTypes
            .AsNoTracking()
            .Where(t => t.ClientId == 0 || t.ClientId == id)
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var rules = await db.SlaRules
            .AsNoTracking()
            .Where(r => r.ClientId == id)
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

    [HttpPost("clients/{id:long}/sla-rules")]
    public async Task<ActionResult<SlaRuleDto>> CreateSlaRule(
        long id, [FromBody] CreateSlaRuleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");
        if (!await db.Clients.AnyAsync(c => c.Id == id, ct)) throw new NotFoundException();

        var rule = new SlaRule
        {
            ClientId           = id,
            EventTypeId        = req.EventTypeId,
            Name               = req.Name.Trim(),
            InvestigationHours = req.InvestigationHours,
            ClosureHours       = req.ClosureHours,
            CreatedAt          = DateTimeOffset.UtcNow,
        };

        db.SlaRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Ok(new SlaRuleDto(rule.Id, rule.EventTypeId, null, rule.Name, rule.InvestigationHours, rule.ClosureHours));
    }

    [HttpPut("clients/{id:long}/sla-rules/{ruleId:long}")]
    public async Task<IActionResult> UpdateSlaRule(
        long id, long ruleId, [FromBody] CreateSlaRuleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");

        var rule = await db.SlaRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ClientId == id, ct);
        if (rule is null) throw new NotFoundException();

        rule.EventTypeId        = req.EventTypeId;
        rule.Name               = req.Name.Trim();
        rule.InvestigationHours = req.InvestigationHours;
        rule.ClosureHours       = req.ClosureHours;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("clients/{id:long}/sla-rules/{ruleId:long}")]
    public async Task<IActionResult> DeleteSlaRule(long id, long ruleId, CancellationToken ct)
    {
        var rule = await db.SlaRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ClientId == id, ct);
        if (rule is null) throw new NotFoundException();

        rule.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Admin Audit Log ──────────────────────────────────────────────────

    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AdminAuditEventDto>>> GetAdminAudit(
        [FromQuery] long? clientId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = db.AuditEvents
            .AsNoTracking()
            .Where(a => a.EntityType == "client"
                     || a.EntityType == "user"
                     || a.EntityType == "user_client_access");

        if (clientId.HasValue && clientId.Value > 0)
            query = query.Where(a => a.ClientId == clientId.Value);

        query = query.OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(ct);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var distinctClientIds = rawItems
            .Where(a => a.ClientId > 0)
            .Select(a => a.ClientId)
            .Distinct()
            .ToList();

        var clientNames = distinctClientIds.Count > 0
            ? await db.Clients
                .IgnoreQueryFilters()
                .Where(c => distinctClientIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<long, string>();

        var items = rawItems.Select(a => new AdminAuditEventDto(
            a.Id, a.ClientId,
            a.ClientId > 0 && clientNames.TryGetValue(a.ClientId, out var cName) ? cName : null,
            a.EntityType, a.EntityId, a.EventType,
            a.UserId, a.UserDisplayName, a.Body, a.CreatedAt))
            .ToList();

        return Ok(new PagedResult<AdminAuditEventDto>(items, total, page, pageSize));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static IReadOnlyList<string> ParseTemplateIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-");
        return slug.Trim('-');
    }
}
