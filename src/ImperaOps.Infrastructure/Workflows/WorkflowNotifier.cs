using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Workflows;

public sealed class WorkflowNotifier : IWorkflowNotifier
{
    private readonly ImperaOpsDbContext _db;
    private readonly INotificationService _notifications;

    public WorkflowNotifier(ImperaOpsDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
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

        foreach (var userId in targets)
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

        if (targets.Count > 0)
            await _db.SaveChangesAsync(ct);
    }
}
