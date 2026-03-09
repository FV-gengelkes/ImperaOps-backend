using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Templates;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Services;

public sealed class ClientAdminService(
    ImperaOpsDbContext db,
    ICounterService counter) : IClientAdminService
{
    public async Task ApplyTemplateAsync(long clientId, string templateId, CancellationToken ct,
        bool seedDemoData = false, long? ownerUserId = null)
    {
        if (!TemplateLibrary.All.TryGetValue(templateId, out var template))
            throw new ArgumentException($"Template '{templateId}' not found.", nameof(templateId));

        var now = DateTimeOffset.UtcNow;

        // ── Event types ──────────────────────────────────────────────────
        var etEntities = template.EventTypes
            .Select(et => new EventType
            {
                ClientId  = clientId,
                Name      = et.Name,
                SortOrder = et.SortOrder,
                IsSystem  = false,
                IsActive  = true,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();

        foreach (var et in etEntities) db.EventTypes.Add(et);
        await db.SaveChangesAsync(ct);

        var eventTypeKeyToId = template.EventTypes
            .Zip(etEntities, (t, e) => (t.Key, e.Id))
            .ToDictionary(x => x.Key, x => x.Id);

        // ── Workflow statuses ────────────────────────────────────────────
        var wsEntities = template.WorkflowStatuses
            .Select(ws => new WorkflowStatus
            {
                ClientId  = clientId,
                Name      = ws.Name,
                Color     = ws.Color,
                IsClosed  = ws.IsClosed,
                SortOrder = ws.SortOrder,
                IsSystem  = false,
                IsActive  = true,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();

        foreach (var ws in wsEntities) db.WorkflowStatuses.Add(ws);
        await db.SaveChangesAsync(ct);

        var statusMap = template.WorkflowStatuses
            .Zip(wsEntities, (t, e) => (t.Key, e.Id))
            .ToDictionary(x => x.Key, x => x.Id);

        // ── Transitions ──────────────────────────────────────────────────
        foreach (var wt in template.WorkflowTransitions)
        {
            db.WorkflowTransitions.Add(new WorkflowTransition
            {
                ClientId     = clientId,
                FromStatusId = wt.FromStatusKey != null ? statusMap[wt.FromStatusKey] : null,
                ToStatusId   = statusMap[wt.ToStatusKey],
                EventTypeId  = null,
                IsDefault    = wt.IsDefault,
                Label        = wt.Label,
                CreatedAt    = now,
            });
        }

        // ── Custom fields ────────────────────────────────────────────────
        foreach (var cf in template.CustomFields)
        {
            db.CustomFields.Add(new CustomField
            {
                ClientId   = clientId,
                Name       = cf.Name,
                DataType   = cf.DataType,
                IsRequired = cf.IsRequired,
                SortOrder  = cf.SortOrder,
                IsActive   = true,
                Options    = cf.Options,
                CreatedAt  = now,
                UpdatedAt  = now,
            });
        }

        // ── SLA rules ──────────────────────────────────────────────────
        foreach (var sla in template.SlaRules)
        {
            db.SlaRules.Add(new SlaRule
            {
                ClientId           = clientId,
                Name               = sla.Name,
                EventTypeId        = sla.EventTypeKey != null ? eventTypeKeyToId[sla.EventTypeKey] : null,
                InvestigationHours = sla.InvestigationHours,
                ClosureHours       = sla.ClosureHours,
                CreatedAt          = now,
            });
        }

        await db.SaveChangesAsync(ct);

        // ── Demo data ──────────────────────────────────────────────────
        if (seedDemoData)
        {
            await DemoDataGenerator.GenerateDemoDataAsync(
                db, counter, clientId, templateId,
                eventTypeKeyToId, statusMap,
                ownerUserId, ct);
        }
    }

    public async Task<int> PurgeEventDataAsync(long clientId, CancellationToken ct)
    {
        var eventIds = await db.Events.IgnoreQueryFilters()
            .Where(e => e.ClientId == clientId).Select(e => e.Id).ToListAsync(ct);

        if (eventIds.Count > 0)
        {
            // Delete in dependency order (children first)
            // Investigation children
            await db.Database.ExecuteSqlRawAsync(
                "DELETE w FROM InvestigationWitnesses w INNER JOIN Investigations i ON w.InvestigationId = i.Id WHERE i.ClientId = {0}", [clientId], ct);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE e FROM InvestigationEvidence e INNER JOIN Investigations i ON e.InvestigationId = i.Id WHERE i.ClientId = {0}", [clientId], ct);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Investigations WHERE ClientId = {0}", [clientId], ct);

            // Event links
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM EventLinks WHERE ClientId = {0}", [clientId], ct);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM EventLinkGroups WHERE ClientId = {0}", [clientId], ct);

            // Document references for events
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM DocumentReferences WHERE ClientId = {0} AND EntityType = 'event'", [clientId], ct);

            // Tasks, attachments, custom field values (entity-linked)
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Attachments WHERE ClientId = {0}", [clientId], ct);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Tasks WHERE EventId IN (SELECT Id FROM Events WHERE ClientId = {0})", [clientId], ct);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM CustomFieldValues WHERE EntityId IN (SELECT Id FROM Events WHERE ClientId = {0})", [clientId], ct);

            // Events themselves
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Events WHERE ClientId = {0}", [clientId], ct);
        }

        return eventIds.Count;
    }
}
