using Hangfire;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Jobs;

/// <summary>
/// Daily job: finds tasks due within 48 h or overdue up to 7 days,
/// sends one in-app notification + one email per task. Deduplicates
/// via the Notifications table (23-hour window).
/// Scheduled via Hangfire: "0 15 * * *" (15:00 UTC = 9 AM CST).
/// </summary>
public sealed class TaskReminderJob
{
    private readonly ImperaOpsDbContext _db;
    private readonly IEmailService      _email;
    private readonly string             _baseUrl;
    private readonly ILogger<TaskReminderJob> _logger;

    public TaskReminderJob(
        ImperaOpsDbContext db,
        IEmailService email,
        IConfiguration config,
        ILogger<TaskReminderJob> logger)
    {
        _db      = db;
        _email   = email;
        _baseUrl = config["App:BaseUrl"] ?? "http://localhost:3000";
        _logger  = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("TaskReminderJob: starting daily run");

        var now    = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(2);
        var floor  = now.AddDays(-7);

        var tasks = await _db.Tasks
            .AsNoTracking()
            .Where(t => !t.IsComplete
                     && t.AssignedToUserId != null
                     && t.DueAt != null
                     && t.DueAt >= floor
                     && t.DueAt <= cutoff)
            .Join(_db.Events,
                  t => t.EventId,
                  e => e.Id,
                  (t, e) => new { Task = t, EventPublicId = e.PublicId, EventTitle = e.Title })
            .ToListAsync(ct);

        _logger.LogInformation("TaskReminderJob: {Count} tasks in reminder window", tasks.Count);

        foreach (var row in tasks)
        {
            var userId = row.Task.AssignedToUserId!.Value;

            // Deduplication: skip if notified for this task in the last 23 h
            var alreadySent = await _db.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.UserId == userId
                            && n.NotificationType == "task_due_reminder"
                            && n.SubEntityPublicId == row.Task.PublicId
                            && n.CreatedAt > now.AddHours(-23), ct);
            if (alreadySent) continue;

            // Notification preferences (default both on)
            var pref = await _db.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId
                                       && p.NotificationType == "task_due_reminder", ct);
            var inApp   = pref?.InAppEnabled ?? true;
            var doEmail = pref?.EmailEnabled ?? true;

            var isOverdue = row.Task.DueAt!.Value < now;
            var title = isOverdue
                ? $"Overdue: {row.Task.Title}"
                : $"Due {(row.Task.DueAt.Value < now.AddHours(24) ? "today" : "tomorrow")}: {row.Task.Title}";
            var body = $"Task \"{row.Task.Title}\" on {row.EventPublicId} is {(isOverdue ? "overdue" : "due soon")}.";

            if (inApp)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId            = userId,
                    ClientId          = row.Task.ClientId,
                    NotificationType  = "task_due_reminder",
                    Title             = title,
                    Body              = body,
                    EntityPublicId    = row.EventPublicId,
                    SubEntityPublicId = row.Task.PublicId,
                    IsRead            = false,
                    CreatedAt         = DateTimeOffset.UtcNow,
                });
                await _db.SaveChangesAsync(ct);
            }

            if (doEmail)
            {
                var user = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Email, u.DisplayName })
                    .FirstOrDefaultAsync(ct);

                if (user is not null)
                {
                    var eventUrl = $"{_baseUrl}/events/{row.EventPublicId}/details";
                    await _email.SendTaskDueReminderAsync(
                        user.Email, user.DisplayName,
                        row.Task.Title, row.EventPublicId, row.EventTitle,
                        row.Task.DueAt!.Value, isOverdue,
                        eventUrl, CancellationToken.None);
                }
            }
        }

        _logger.LogInformation("TaskReminderJob: done");
    }
}
