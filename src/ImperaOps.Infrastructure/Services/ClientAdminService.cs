using System.Text.Json;
using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Templates;
using ImperaOps.Infrastructure.Workflows;
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

        // ── Event types (skip if already exist for this client) ────────
        var existingEventTypes = await db.EventTypes
            .Where(e => e.ClientId == clientId)
            .Select(e => e.Name)
            .ToListAsync(ct);
        var existingEtNames = new HashSet<string>(existingEventTypes, StringComparer.OrdinalIgnoreCase);

        var etEntities = template.EventTypes
            .Where(et => !existingEtNames.Contains(et.Name))
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

        // Build key→id map from ALL client event types (existing + newly created)
        var allClientEventTypes = await db.EventTypes
            .Where(e => e.ClientId == clientId)
            .Select(e => new { e.Name, e.Id })
            .ToListAsync(ct);
        var etNameToId = allClientEventTypes.ToDictionary(e => e.Name, e => e.Id, StringComparer.OrdinalIgnoreCase);
        var eventTypeKeyToId = template.EventTypes
            .Where(t => etNameToId.ContainsKey(t.Name))
            .ToDictionary(t => t.Key, t => etNameToId[t.Name]);

        // ── Workflow statuses (skip if already exist for this client) ───
        var existingStatuses = await db.WorkflowStatuses
            .Where(s => s.ClientId == clientId)
            .Select(s => s.Name)
            .ToListAsync(ct);
        var existingWsNames = new HashSet<string>(existingStatuses, StringComparer.OrdinalIgnoreCase);

        var wsEntities = template.WorkflowStatuses
            .Where(ws => !existingWsNames.Contains(ws.Name))
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

        // Build key→id map from ALL client workflow statuses (existing + newly created)
        var allClientStatuses = await db.WorkflowStatuses
            .Where(s => s.ClientId == clientId)
            .Select(s => new { s.Name, s.Id })
            .ToListAsync(ct);
        var wsNameToId = allClientStatuses.ToDictionary(s => s.Name, s => s.Id, StringComparer.OrdinalIgnoreCase);
        var statusMap = template.WorkflowStatuses
            .Where(t => wsNameToId.ContainsKey(t.Name))
            .ToDictionary(t => t.Key, t => wsNameToId[t.Name]);

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

        // ── Workflow rules ─────────────────────────────────────────────
        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        foreach (var wr in template.WorkflowRules)
        {
            // Resolve condition values from template keys → real IDs
            var conditions = wr.Conditions.Select(c =>
            {
                var value = c.Value;
                if (value != null)
                {
                    if (c.Field == "event_type_id")
                    {
                        // Could be comma-separated for "in" operator
                        value = string.Join(",", value.Split(',')
                            .Select(k => eventTypeKeyToId.TryGetValue(k.Trim(), out var id) ? id.ToString() : k.Trim()));
                    }
                    else if (c.Field == "workflow_status_id")
                    {
                        value = string.Join(",", value.Split(',')
                            .Select(k => statusMap.TryGetValue(k.Trim(), out var id) ? id.ToString() : k.Trim()));
                    }
                }
                return new WorkflowCondition { Field = c.Field, Operator = c.Operator, Value = value };
            }).ToList();

            // Resolve action configs from template keys → real IDs
            var actions = wr.Actions.Select(a =>
            {
                var config = new WorkflowActionConfig();
                if (a.StatusKey != null && statusMap.TryGetValue(a.StatusKey, out var statusId))
                    config.WorkflowStatusId = statusId;
                if (a.TaskTitle != null) config.TaskTitle = a.TaskTitle;
                if (a.TaskDescription != null) config.TaskDescription = a.TaskDescription;
                if (a.TaskDueDaysFromNow != null) config.TaskDueDaysFromNow = a.TaskDueDaysFromNow;
                if (a.NotifyRoles != null) config.NotifyRoles = a.NotifyRoles;
                if (a.NotificationMessage != null) config.NotificationMessage = a.NotificationMessage;
                if (a.CommentBody != null) config.CommentBody = a.CommentBody;
                return new WorkflowAction { Type = a.Type, Config = config };
            }).ToList();

            db.WorkflowRules.Add(new WorkflowRule
            {
                ClientId       = clientId,
                Name           = wr.Name,
                Description    = wr.Description,
                TriggerType    = wr.TriggerType,
                SortOrder      = wr.SortOrder,
                StopOnMatch    = wr.StopOnMatch,
                IsActive       = true,
                IsSeedData     = true,
                ConditionsJson = JsonSerializer.Serialize(conditions, jsonOpts),
                ActionsJson    = JsonSerializer.Serialize(actions, jsonOpts),
                CreatedAt      = now,
                UpdatedAt      = now,
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
