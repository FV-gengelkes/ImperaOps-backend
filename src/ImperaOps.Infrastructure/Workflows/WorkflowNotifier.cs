using Hangfire;
using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using ImperaOps.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ImperaOps.Infrastructure.Workflows;

public sealed class WorkflowNotifier : IWorkflowNotifier
{
    private readonly ImperaOpsDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IBackgroundJobClient _jobs;
    private readonly INotificationPushService _push;
    private readonly string _baseUrl;

    public WorkflowNotifier(
        ImperaOpsDbContext db,
        INotificationService notifications,
        IBackgroundJobClient jobs,
        INotificationPushService push,
        IConfiguration config)
    {
        _db            = db;
        _notifications = notifications;
        _jobs          = jobs;
        _push          = push;
        _baseUrl       = config["App:BaseUrl"] ?? "http://localhost:3000";
    }

    public async Task NotifyEventAssignedAsync(long userId, long clientId, string eventPublicId, string eventTitle, CancellationToken ct)
    {
        await _notifications.NotifyEventAssignedAsync(
            userId, 0, "Workflow Automation", clientId, eventPublicId, eventTitle, ct);
    }

    public async Task NotifyUsersAsync(
        long clientId, string eventPublicId, string ruleName, string message,
        long[]? userIds, string[]? roles, CancellationToken ct)
    {
        var targets = new HashSet<long>();

        if (userIds is { Length: > 0 })
            foreach (var id in userIds) targets.Add(id);

        if (roles is { Length: > 0 })
        {
            var roleUsers = await _db.UserClientAccess.AsNoTracking()
                .Where(a => a.ClientId == clientId && roles.Contains(a.Role))
                .Select(a => a.UserId)
                .ToListAsync(ct);
            foreach (var id in roleUsers) targets.Add(id);
        }

        var eventUrl = $"{_baseUrl}/events/{eventPublicId}/details";

        foreach (var userId in targets)
        {
            var (emailEnabled, inAppEnabled) = await GetPref(userId, ct);

            if (inAppEnabled)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    ClientId = clientId,
                    NotificationType = "workflow_rule",
                    Title = $"Workflow: {ruleName}",
                    Body = message,
                    EntityPublicId = eventPublicId,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }

            if (emailEnabled)
            {
                var user = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Email, u.DisplayName })
                    .FirstOrDefaultAsync(ct);
                if (user is not null)
                {
                    _jobs.Enqueue<IEmailService>(x =>
                        x.SendWorkflowRuleAsync(user.Email, user.DisplayName, ruleName, message, eventPublicId, eventUrl, CancellationToken.None));
                }
            }
        }

        if (targets.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            foreach (var userId in targets) _push.Push(userId, "refresh");
        }
    }

    private async Task<(bool email, bool inApp)> GetPref(long userId, CancellationToken ct)
    {
        var pref = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == "workflow_rule", ct);
        return pref is null ? (true, true) : (pref.EmailEnabled, pref.InAppEnabled);
    }
}
