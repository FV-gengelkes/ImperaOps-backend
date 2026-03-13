using Hangfire;
using ImperaOps.Application.Abstractions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Jobs;

/// <summary>
/// Daily job: checks all enabled report schedules and sends email digests
/// to Manager + Admin users when the schedule matches today.
/// Scheduled via Hangfire: "0 7 * * *" (07:00 UTC).
/// </summary>
public sealed class ScheduledReportJob
{
    private readonly ImperaOpsDbContext   _db;
    private readonly IEventReadRepository _readRepo;
    private readonly IEmailService        _email;
    private readonly string               _baseUrl;
    private readonly ILogger<ScheduledReportJob> _logger;

    public ScheduledReportJob(
        ImperaOpsDbContext db,
        IEventReadRepository readRepo,
        IEmailService email,
        IConfiguration config,
        ILogger<ScheduledReportJob> logger)
    {
        _db       = db;
        _readRepo = readRepo;
        _email    = email;
        _baseUrl  = config["App:BaseUrl"] ?? "http://localhost:3000";
        _logger   = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("ScheduledReportJob: starting daily run");

        var today = DateTime.UtcNow;
        var todayDow = (int)today.DayOfWeek; // 0=Sun..6=Sat

        var schedules = await _db.ReportSchedules
            .Where(s => s.IsEnabled)
            .ToListAsync(ct);

        _logger.LogInformation("ScheduledReportJob: {Count} enabled schedules", schedules.Count);

        foreach (var schedule in schedules)
        {
            // Check if today matches the schedule
            bool matches = schedule.Frequency switch
            {
                "weekly"  => todayDow == schedule.DayOfWeek,
                "monthly" => today.Day == schedule.DayOfMonth,
                _         => false,
            };
            if (!matches) continue;

            // Idempotency: skip if already sent today
            if (schedule.LastSentAt.HasValue && schedule.LastSentAt.Value.Date == today.Date)
                continue;

            try
            {
                await SendReportForClientAsync(schedule, today, ct);
                schedule.LastSentAt = today;
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduledReportJob: failed for client {ClientId}", schedule.ClientId);
            }
        }

        _logger.LogInformation("ScheduledReportJob: done");
    }

    private async Task SendReportForClientAsync(
        ImperaOps.Domain.Entities.ReportSchedule schedule, DateTime today, CancellationToken ct)
    {
        var clientId = schedule.ClientId;

        // Get client name
        var client = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new { c.Name })
            .FirstOrDefaultAsync(ct);
        if (client is null) return;

        // Date range
        var daysBack = schedule.Frequency == "monthly" ? 30 : 7;
        var dateFrom = today.AddDays(-daysBack).Date;
        var dateTo   = today.Date;

        // Get analytics
        var analytics = await _readRepo.GetAnalyticsAsync(
            [clientId], dateFrom, dateTo, ct);

        // Resolve Manager + Admin users for this client
        var recipients = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.ClientId == clientId
                     && (a.Role == "Admin" || a.Role == "Manager"))
            .Join(_db.Users.Where(u => u.IsActive),
                  a => a.UserId,
                  u => u.Id,
                  (a, u) => new { u.Id, u.Email, u.DisplayName })
            .ToListAsync(ct);

        if (recipients.Count == 0) return;

        // Prepare data for email
        var byType = analytics.ByType
            .Select(t => (t.EventTypeName, t.Count))
            .ToList();

        var topLocations = analytics.TopLocations
            .Take(5)
            .Select(l => (l.Location, l.Count))
            .ToList();

        var dashboardUrl = $"{_baseUrl}/dashboard";

        foreach (var user in recipients)
        {
            // Check notification preference
            var pref = await _db.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id
                                       && p.NotificationType == "scheduled_report", ct);
            var emailEnabled = pref?.EmailEnabled ?? true;
            if (!emailEnabled) continue;

            try
            {
                await _email.SendScheduledReportAsync(
                    user.Email, user.DisplayName, client.Name, schedule.Frequency,
                    analytics.Total, analytics.Open, analytics.Closed,
                    analytics.AvgResolutionDays, analytics.SlaBreachedCount,
                    byType, topLocations, dashboardUrl, ct);

                _logger.LogInformation("ScheduledReportJob: sent {Freq} report to {Email} for client {ClientId}",
                    schedule.Frequency, user.Email, clientId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScheduledReportJob: email send failed for {Email}", user.Email);
            }
        }
    }
}
