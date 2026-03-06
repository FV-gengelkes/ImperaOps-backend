using Hangfire;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Jobs;

/// <summary>
/// Scans open events and sends notifications when events breach their SLA window.
/// Scheduled via Hangfire: every 30 minutes.
/// </summary>
public sealed class SlaEscalationJob
{
    private readonly ImperaOpsDbContext      _db;
    private readonly ILogger<SlaEscalationJob> _logger;

    public SlaEscalationJob(ImperaOpsDbContext db, ILogger<SlaEscalationJob> logger)
    {
        _db     = db;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var rules = await _db.SlaRules.AsNoTracking().ToListAsync(ct);
        if (rules.Count == 0)
        {
            _logger.LogDebug("SlaEscalationJob: no rules configured.");
            return;
        }

        var closedStatusIds = await _db.WorkflowStatuses
            .AsNoTracking().Where(s => s.IsClosed).Select(s => s.Id).ToListAsync(ct);

        var openEvents = await _db.Events
            .AsNoTracking()
            .Where(e => !closedStatusIds.Contains(e.WorkflowStatusId))
            .ToListAsync(ct);

        var clientAdminMap = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.Role == "Admin" || a.Role == "Manager")
            .GroupBy(a => a.ClientId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(a => a.UserId).ToList(), ct);

        // Dedup: set of "type:publicId" that already have a notification
        var existingSet = (await _db.Notifications
            .AsNoTracking()
            .Where(n => n.NotificationType == "sla_investigation_breach" || n.NotificationType == "sla_closure_breach")
            .Select(n => n.NotificationType + ":" + n.EntityPublicId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var toAdd = new List<Notification>();

        foreach (var ev in openEvents)
        {
            var rule = rules.FirstOrDefault(r => r.ClientId == ev.ClientId && r.EventTypeId == ev.EventTypeId)
                    ?? rules.FirstOrDefault(r => r.ClientId == ev.ClientId && r.EventTypeId == null);

            if (rule == null) continue;

            var adminIds = clientAdminMap.TryGetValue(ev.ClientId, out var ids) ? ids : new List<long>();
            if (adminIds.Count == 0) continue;

            // Investigation breach
            if (rule.InvestigationHours.HasValue && ev.OwnerUserId == null)
            {
                var deadline = ev.CreatedAt.AddHours(rule.InvestigationHours.Value);
                var key = $"sla_investigation_breach:{ev.PublicId}";
                if (now > deadline && !existingSet.Contains(key))
                {
                    foreach (var adminId in adminIds)
                        toAdd.Add(MakeNotif(adminId, ev, "sla_investigation_breach",
                            $"SLA Breach: {ev.PublicId}",
                            $"Event \"{ev.Title}\" ({ev.PublicId}) has no owner assigned after {rule.InvestigationHours}h (investigation SLA).", now));
                    existingSet.Add(key);
                    _logger.LogInformation("SlaEscalationJob: investigation breach {PublicId}", ev.PublicId);
                }
            }

            // Closure breach
            if (rule.ClosureHours.HasValue)
            {
                var deadline = ev.CreatedAt.AddHours(rule.ClosureHours.Value);
                var key = $"sla_closure_breach:{ev.PublicId}";
                if (now > deadline && !existingSet.Contains(key))
                {
                    foreach (var adminId in adminIds)
                        toAdd.Add(MakeNotif(adminId, ev, "sla_closure_breach",
                            $"SLA Breach: {ev.PublicId}",
                            $"Event \"{ev.Title}\" ({ev.PublicId}) is still open after {rule.ClosureHours}h (closure SLA).", now));
                    existingSet.Add(key);
                    _logger.LogInformation("SlaEscalationJob: closure breach {PublicId}", ev.PublicId);
                }
            }
        }

        if (toAdd.Count > 0)
        {
            _db.Notifications.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("SlaEscalationJob: added {Count} breach notifications.", toAdd.Count);
        }
    }

    private static Notification MakeNotif(
        long userId, Event ev, string type, string title, string body, DateTimeOffset now)
        => new()
        {
            UserId           = userId,
            ClientId         = ev.ClientId,
            NotificationType = type,
            Title            = title,
            Body             = body,
            EntityPublicId   = ev.PublicId,
            IsRead           = false,
            CreatedAt        = now,
        };
}
