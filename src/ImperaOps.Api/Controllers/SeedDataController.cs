using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:long}/seed-data")]
[Authorize]
public sealed class SeedDataController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public SeedDataController(ImperaOpsDbContext db) => _db = db;

    /// <summary>Count all seed data records for this client.</summary>
    [HttpGet("count")]
    public async Task<IActionResult> Count(long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var counts = new Dictionary<string, int>
        {
            ["events"] = await _db.Events.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["tasks"] = await _db.Tasks.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["auditEvents"] = await _db.AuditEvents.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["customFieldValues"] = await _db.CustomFieldValues.IgnoreQueryFilters().CountAsync(x => x.IsSeedData && x.DeletedAt == null, ct),
            ["investigations"] = await _db.Investigations.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["eventTypes"] = await _db.EventTypes.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["workflowStatuses"] = await _db.WorkflowStatuses.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["workflowTransitions"] = await _db.WorkflowTransitions.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["customFields"] = await _db.CustomFields.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["slaRules"] = await _db.SlaRules.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["rootCauses"] = await _db.RootCauseTaxonomyItems.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["workflowRules"] = await _db.WorkflowRules.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["webhooks"] = await _db.ClientWebhooks.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
            ["notifications"] = await _db.Notifications.IgnoreQueryFilters().CountAsync(x => x.ClientId == clientId && x.IsSeedData && x.DeletedAt == null, ct),
        };

        return Ok(new { counts, total = counts.Values.Sum() });
    }

    /// <summary>Permanently delete all seed data for this client.</summary>
    [HttpDelete]
    public async Task<IActionResult> Purge(long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        // Delete in dependency order: children first, parents last.
        // Use raw SQL for efficiency — bypasses soft-delete filters and change tracking.
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(ct);

        try
        {
            // Get seed event IDs for child table cleanup
            var seedEventIds = await _db.Events.IgnoreQueryFilters()
                .Where(e => e.ClientId == clientId && e.IsSeedData)
                .Select(e => e.Id)
                .ToListAsync(ct);

            var seedInvestigationIds = await _db.Investigations.IgnoreQueryFilters()
                .Where(i => i.ClientId == clientId && i.IsSeedData)
                .Select(i => i.Id)
                .ToListAsync(ct);

            int total = 0;

            // 1. Investigation children (no ClientId — join via InvestigationId)
            if (seedInvestigationIds.Count > 0)
            {
                var invIds = string.Join(",", seedInvestigationIds);
                total += await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM InvestigationWitnesses WHERE InvestigationId IN (" + invIds + ")", ct);
                total += await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM InvestigationEvidence WHERE InvestigationId IN (" + invIds + ")", ct);
            }

            // 2. Event children
            if (seedEventIds.Count > 0)
            {
                var evIds = string.Join(",", seedEventIds);
                total += await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Tasks WHERE EventId IN (" + evIds + ")", ct);
                total += await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Attachments WHERE EntityType = 'event' AND EntityId IN (" + evIds + ")", ct);
                total += await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM DocumentReferences WHERE EntityType = 'event' AND EntityId IN (" + evIds + ")", ct);
                total += await _db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM EventLinks WHERE ClientId = " + clientId + " AND EventId IN (" + evIds + ")", ct);
            }

            // 3. Seed-flagged tables with ClientId
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM CustomFieldValues WHERE IsSeedData = 1 AND EntityId IN (SELECT Id FROM Events WHERE ClientId = {clientId})", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM AuditEvents WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM Notifications WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM InsightAlerts WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM Investigations WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM Events WHERE ClientId = {clientId} AND IsSeedData = 1", ct);

            // 4. Config tables
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM EventLinkGroups WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM WorkflowTransitions WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM WorkflowRules WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM SlaRules WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM RootCauseTaxonomyItems WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM CustomFields WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM ClientWebhooks WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM ClientDocuments WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM EventTypes WHERE ClientId = {clientId} AND IsSeedData = 1", ct);
            total += await _db.Database.ExecuteSqlAsync(
                $"DELETE FROM WorkflowStatuses WHERE ClientId = {clientId} AND IsSeedData = 1", ct);

            return Ok(new { purged = true, totalRecordsDeleted = total });
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
