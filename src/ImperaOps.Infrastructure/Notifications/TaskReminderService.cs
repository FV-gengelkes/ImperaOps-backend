using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Notifications;

public sealed class TaskReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskReminderService> _logger;

    public TaskReminderService(IServiceScopeFactory scopeFactory, ILogger<TaskReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitUntilNextRunAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do { await RunAsync(stoppingToken); }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Delays until the next 15:00 UTC (9:00 AM CST / 10:00 AM CDT).</summary>
    private static async Task WaitUntilNextRunAsync(CancellationToken ct)
    {
        var now  = DateTimeOffset.UtcNow;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, 15, 0, 0, TimeSpan.Zero);
        if (next <= now)
            next = next.AddDays(1);

        var delay = next - now;
        await Task.Delay(delay, ct);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("TaskReminderService: starting daily run");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db    = scope.ServiceProvider.GetRequiredService<ImperaOpsDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var baseUrl = config["App:BaseUrl"] ?? "http://localhost:3000";

            var now    = DateTimeOffset.UtcNow;
            var cutoff = now.AddDays(2);   // due within 48 h
            var floor  = now.AddDays(-7);  // overdue, but no older than 7 days

            var tasks = await db.Tasks
                .AsNoTracking()
                .Where(t => !t.IsComplete
                         && t.AssignedToUserId != null
                         && t.DueAt != null
                         && t.DueAt >= floor
                         && t.DueAt <= cutoff)
                .Join(db.Events,
                      t => t.EventId,
                      e => e.Id,
                      (t, e) => new { Task = t, EventPublicId = e.PublicId, EventTitle = e.Title })
                .ToListAsync(ct);

            _logger.LogInformation("TaskReminderService: {Count} tasks in reminder window", tasks.Count);

            foreach (var row in tasks)
            {
                var userId = row.Task.AssignedToUserId!.Value;

                // Deduplication: skip if notified for this task in the last 23 h
                var alreadySent = await db.Notifications
                    .AsNoTracking()
                    .AnyAsync(n => n.UserId == userId
                                && n.NotificationType == "task_due_reminder"
                                && n.SubEntityPublicId == row.Task.PublicId
                                && n.CreatedAt > now.AddHours(-23), ct);
                if (alreadySent) continue;

                // Notification preferences (default both on)
                var pref = await db.NotificationPreferences
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
                    db.Notifications.Add(new Notification
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
                    await db.SaveChangesAsync(ct);
                }

                if (doEmail)
                {
                    var user = await db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.Email, u.DisplayName })
                        .FirstOrDefaultAsync(ct);

                    if (user is not null)
                    {
                        try
                        {
                            var eventUrl = $"{baseUrl}/events/{row.EventPublicId}/details";
                            await email.SendTaskDueReminderAsync(
                                user.Email, user.DisplayName,
                                row.Task.Title, row.EventPublicId, row.EventTitle,
                                row.Task.DueAt!.Value, isOverdue,
                                eventUrl, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send task_due_reminder email for task {TaskPublicId}", row.Task.PublicId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TaskReminderService run failed");
        }
    }
}
