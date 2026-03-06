using Hangfire;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly ImperaOpsDbContext _db;
    private readonly IBackgroundJobClient _jobs;
    private readonly INotificationPushService _push;
    private readonly string _baseUrl;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ImperaOpsDbContext db, IBackgroundJobClient jobs, INotificationPushService push, IConfiguration config, ILogger<NotificationService> logger)
    {
        _db      = db;
        _jobs    = jobs;
        _push    = push;
        _baseUrl = config["App:BaseUrl"] ?? "http://localhost:3000";
        _logger  = logger;
    }

    // ── Public methods ─────────────────────────────────────────────────────────

    public async Task NotifyEventAssignedAsync(long newOwnerUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string eventTitle, CancellationToken ct = default)
    {
        if (newOwnerUserId == actorUserId) return;

        var (emailEnabled, inAppEnabled) = await GetPref(newOwnerUserId, "event_assigned", ct);
        var title = $"{eventPublicId} assigned to you";
        var body  = $"{actorName} assigned event \"{eventTitle}\" to you.";

        if (inAppEnabled)
        {
            AddInApp(newOwnerUserId, clientId, "event_assigned", title, body, eventPublicId);
            await _db.SaveChangesAsync(ct);
            _push.Push(newOwnerUserId, "refresh");
        }

        if (emailEnabled)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == newOwnerUserId)
                .Select(u => new { u.Email, u.DisplayName })
                .FirstOrDefaultAsync(ct);
            if (user is not null)
            {
                var url = EventUrl(eventPublicId);
                _jobs.Enqueue<IEmailService>(x =>
                    x.SendEventAssignedAsync(user.Email, user.DisplayName, actorName, eventPublicId, eventTitle, url, CancellationToken.None));
            }
        }
    }

    public async Task NotifyTaskAssignedAsync(long assignedToUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string taskPublicId, string taskTitle, CancellationToken ct = default)
    {
        // Soft-delete any existing notification for this task before creating a fresh one
        await _db.Notifications
            .Where(n => n.SubEntityPublicId == taskPublicId && n.NotificationType == "task_assigned")
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.DeletedAt, DateTimeOffset.UtcNow), ct);

        var (emailEnabled, inAppEnabled) = await GetPref(assignedToUserId, "task_assigned", ct);
        var title = $"Task assigned: {taskTitle}";
        var body  = $"{actorName} assigned task \"{taskTitle}\" to you.";

        if (inAppEnabled)
        {
            AddInApp(assignedToUserId, clientId, "task_assigned", title, body, eventPublicId, taskPublicId);
            await _db.SaveChangesAsync(ct);
            _push.Push(assignedToUserId, "refresh");
        }

        if (emailEnabled)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == assignedToUserId)
                .Select(u => new { u.Email, u.DisplayName })
                .FirstOrDefaultAsync(ct);
            if (user is not null)
            {
                var url = EventUrl(eventPublicId);
                _jobs.Enqueue<IEmailService>(x =>
                    x.SendTaskAssignedAsync(user.Email, user.DisplayName, actorName, taskTitle, eventPublicId, url, CancellationToken.None));
            }
        }
    }

    public async Task NotifyCommentAddedAsync(long eventOwnerUserId, long reportedByUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string eventTitle, string commentSnippet, IReadOnlyList<long>? mentionedUserIds = null, CancellationToken ct = default)
    {
        var recipients = new HashSet<long>();
        if (eventOwnerUserId > 0) recipients.Add(eventOwnerUserId);
        if (reportedByUserId > 0) recipients.Add(reportedByUserId);
        if (mentionedUserIds is not null)
            foreach (var uid in mentionedUserIds) if (uid > 0) recipients.Add(uid);
        recipients.Remove(actorUserId);

        if (recipients.Count == 0) return;

        var snippet = commentSnippet.Length > 120 ? commentSnippet[..120] + "…" : commentSnippet;
        var title   = $"New comment on {eventPublicId}";
        var body    = $"{actorName} commented on \"{eventTitle}\": {snippet}";

        // Add all in-app notifications first, then save once
        var inAppRecipients = new List<long>();
        foreach (var userId in recipients)
        {
            var (_, inAppEnabled) = await GetPref(userId, "comment_added", ct);
            if (inAppEnabled) { AddInApp(userId, clientId, "comment_added", title, body, eventPublicId); inAppRecipients.Add(userId); }
        }
        await _db.SaveChangesAsync(ct);
        foreach (var uid in inAppRecipients) _push.Push(uid, "refresh");

        // Then send emails per recipient
        foreach (var userId in recipients)
        {
            var (emailEnabled, _) = await GetPref(userId, "comment_added", ct);
            if (!emailEnabled) continue;

            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Email, u.DisplayName })
                .FirstOrDefaultAsync(ct);
            if (user is not null)
            {
                var url = EventUrl(eventPublicId);
                _jobs.Enqueue<IEmailService>(x =>
                    x.SendCommentAddedAsync(user.Email, user.DisplayName, actorName, eventPublicId, eventTitle, snippet, url, CancellationToken.None));
            }
        }
    }

    public async Task NotifyStatusChangedAsync(long eventOwnerUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string eventTitle, string newStatusName, CancellationToken ct = default)
    {
        if (eventOwnerUserId == 0 || eventOwnerUserId == actorUserId) return;

        var (emailEnabled, inAppEnabled) = await GetPref(eventOwnerUserId, "status_changed", ct);
        var title = $"{eventPublicId} status changed to {newStatusName}";
        var body  = $"{actorName} changed the status of \"{eventTitle}\" to \"{newStatusName}\".";

        if (inAppEnabled)
        {
            AddInApp(eventOwnerUserId, clientId, "status_changed", title, body, eventPublicId);
            await _db.SaveChangesAsync(ct);
            _push.Push(eventOwnerUserId, "refresh");
        }

        if (emailEnabled)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == eventOwnerUserId)
                .Select(u => new { u.Email, u.DisplayName })
                .FirstOrDefaultAsync(ct);
            if (user is not null)
            {
                var url = EventUrl(eventPublicId);
                _jobs.Enqueue<IEmailService>(x =>
                    x.SendStatusChangedAsync(user.Email, user.DisplayName, actorName, eventPublicId, eventTitle, newStatusName, url, CancellationToken.None));
            }
        }
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    private async Task<(bool email, bool inApp)> GetPref(long userId, string type, CancellationToken ct)
    {
        var pref = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == type, ct);
        return pref is null ? (true, true) : (pref.EmailEnabled, pref.InAppEnabled);
    }

    public async Task ClearTaskNotificationAsync(string taskPublicId, CancellationToken ct = default)
    {
        await _db.Notifications
            .Where(n => n.SubEntityPublicId == taskPublicId && n.NotificationType == "task_assigned")
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.DeletedAt, DateTimeOffset.UtcNow), ct);
    }

    private void AddInApp(long userId, long clientId, string type, string title, string body, string? entityPublicId, string? subEntityPublicId = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId            = userId,
            ClientId          = clientId,
            NotificationType  = type,
            Title             = title,
            Body              = body,
            EntityPublicId    = entityPublicId,
            SubEntityPublicId = subEntityPublicId,
            IsRead            = false,
            CreatedAt         = DateTimeOffset.UtcNow,
        });
    }

    private string EventUrl(string publicId) => $"{_baseUrl}/events/{publicId}/details";
}
