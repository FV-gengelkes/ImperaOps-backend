using Dapper;
using Hangfire;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Ai;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace ImperaOps.Infrastructure.Jobs;

public sealed class InsightDetectionJob
{
    private readonly ImperaOpsDbContext _db;
    private readonly string _cs;
    private readonly ILogger<InsightDetectionJob> _logger;
    private readonly IClaudeService _ai;

    public InsightDetectionJob(ImperaOpsDbContext db, IConfiguration config, ILogger<InsightDetectionJob> logger, IClaudeService ai)
    {
        _db = db;
        _cs = config.GetConnectionString("Database")!;
        _logger = logger;
        _ai = ai;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var clientIds = await _db.Clients.AsNoTracking()
            .Where(c => c.Status != "Inactive")
            .Select(c => c.Id)
            .ToListAsync(ct);

        var alerts = new List<InsightAlert>();

        await using var conn = new MySqlConnection(_cs);
        await conn.OpenAsync(ct);

        foreach (var clientId in clientIds)
        {
            alerts.AddRange(await DetectSpikes(conn, clientId, now, ct));
            alerts.AddRange(await DetectLocationHotspots(conn, clientId, now, ct));
            alerts.AddRange(await DetectRecurringPerson(conn, clientId, now, ct));
            alerts.AddRange(await DetectRecurringLocationType(conn, clientId, now, ct));
        }

        // Dedup: skip if identical (ClientId, AlertType, Title) generated in last 24h
        if (alerts.Count > 0)
        {
            var existing = await _db.InsightAlerts.AsNoTracking()
                .Where(a => a.GeneratedAt > now.AddHours(-24))
                .Select(a => new { a.ClientId, a.AlertType, a.Title })
                .ToListAsync(ct);

            var existingSet = existing.Select(e => $"{e.ClientId}|{e.AlertType}|{e.Title}").ToHashSet();

            var toAdd = alerts.Where(a => !existingSet.Contains($"{a.ClientId}|{a.AlertType}|{a.Title}")).ToList();

            if (toAdd.Count > 0)
            {
                _db.InsightAlerts.AddRange(toAdd);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("InsightDetectionJob: generated {Count} alerts.", toAdd.Count);

                // Try to generate AI summaries for new alerts
                try
                {
                    var alertInfos = toAdd.Select(a => new AlertInfo(a.AlertType, a.Severity, a.Title, a.Body)).ToList();
                    var summary = await _ai.AnalyzeTrendsAsync(alertInfos, ct);
                    foreach (var a in toAdd)
                        a.AiSummary = summary;
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "InsightDetectionJob: AI summary generation failed (non-blocking).");
                }
            }
        }
    }

    private async Task<List<InsightAlert>> DetectSpikes(MySqlConnection conn, long clientId, DateTimeOffset now, CancellationToken ct)
    {
        const string sql = @"
SELECT et.Name AS EventTypeName, e.EventTypeId,
       SUM(CASE WHEN e.OccurredAt >= @WeekStart THEN 1 ELSE 0 END) AS ThisWeek,
       SUM(CASE WHEN e.OccurredAt >= @PriorStart AND e.OccurredAt < @WeekStart THEN 1 ELSE 0 END) AS LastWeek
FROM Events e
JOIN EventTypes et ON et.Id = e.EventTypeId
WHERE e.ClientId = @ClientId AND e.DeletedAt IS NULL AND e.OccurredAt >= @PriorStart
GROUP BY e.EventTypeId, et.Name;";

        var weekStart = now.AddDays(-7);
        var priorStart = now.AddDays(-14);

        var rows = await conn.QueryAsync<(string EventTypeName, long EventTypeId, int ThisWeek, int LastWeek)>(
            new CommandDefinition(sql, new { ClientId = clientId, WeekStart = weekStart, PriorStart = priorStart }, cancellationToken: ct));

        var alerts = new List<InsightAlert>();
        foreach (var r in rows)
        {
            if (r.ThisWeek >= 3 && r.LastWeek > 0)
            {
                var pctIncrease = ((double)(r.ThisWeek - r.LastWeek) / r.LastWeek) * 100;
                if (pctIncrease >= 30)
                {
                    alerts.Add(new InsightAlert
                    {
                        ClientId = clientId,
                        AlertType = "spike",
                        Severity = pctIncrease >= 100 ? "critical" : "warning",
                        Title = $"{r.EventTypeName} events up {pctIncrease:F0}% this week",
                        Body = $"{r.EventTypeName} had {r.ThisWeek} events this week vs {r.LastWeek} last week — a {pctIncrease:F0}% increase.",
                        GeneratedAt = now,
                        ExpiresAt = now.AddDays(7),
                        CreatedAt = now,
                    });
                }
            }
        }
        return alerts;
    }

    private async Task<List<InsightAlert>> DetectLocationHotspots(MySqlConnection conn, long clientId, DateTimeOffset now, CancellationToken ct)
    {
        const string sql = @"
SELECT e.Location, COUNT(*) AS Count
FROM Events e
WHERE e.ClientId = @ClientId AND e.DeletedAt IS NULL
  AND e.OccurredAt >= @Since AND e.Location IS NOT NULL AND e.Location != ''
GROUP BY e.Location;";

        var rows = (await conn.QueryAsync<(string Location, int Count)>(
            new CommandDefinition(sql, new { ClientId = clientId, Since = now.AddDays(-30) }, cancellationToken: ct))).ToList();

        if (rows.Count < 2) return [];

        var avg = rows.Average(r => r.Count);
        var alerts = new List<InsightAlert>();

        foreach (var r in rows)
        {
            if (r.Count >= 3 && r.Count > 2 * avg)
            {
                alerts.Add(new InsightAlert
                {
                    ClientId = clientId,
                    AlertType = "location_hotspot",
                    Severity = r.Count > 3 * avg ? "critical" : "warning",
                    Title = $"{r.Location} incidents up — {r.Count} in 30 days",
                    Body = $"{r.Location} had {r.Count} events in the last 30 days (average across locations: {avg:F1}).",
                    GeneratedAt = now,
                    ExpiresAt = now.AddDays(7),
                    CreatedAt = now,
                });
            }
        }
        return alerts;
    }

    private async Task<List<InsightAlert>> DetectRecurringPerson(MySqlConnection conn, long clientId, DateTimeOffset now, CancellationToken ct)
    {
        const string sql = @"
SELECT e.ExternalReporterName, COUNT(*) AS Count
FROM Events e
WHERE e.ClientId = @ClientId AND e.DeletedAt IS NULL
  AND e.OccurredAt >= @Since AND e.ExternalReporterName IS NOT NULL AND e.ExternalReporterName != ''
GROUP BY e.ExternalReporterName
HAVING COUNT(*) >= 3;";

        var rows = await conn.QueryAsync<(string ExternalReporterName, int Count)>(
            new CommandDefinition(sql, new { ClientId = clientId, Since = now.AddDays(-90) }, cancellationToken: ct));

        return rows.Select(r => new InsightAlert
        {
            ClientId = clientId,
            AlertType = "recurring_person",
            Severity = "info",
            Title = $"Recurring reporter: {r.ExternalReporterName} ({r.Count} events in 90 days)",
            Body = $"{r.ExternalReporterName} has been involved in {r.Count} events in the last 90 days.",
            GeneratedAt = now,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now,
        }).ToList();
    }

    private async Task<List<InsightAlert>> DetectRecurringLocationType(MySqlConnection conn, long clientId, DateTimeOffset now, CancellationToken ct)
    {
        const string sql = @"
SELECT e.Location, et.Name AS EventTypeName, COUNT(*) AS Count
FROM Events e
JOIN EventTypes et ON et.Id = e.EventTypeId
WHERE e.ClientId = @ClientId AND e.DeletedAt IS NULL
  AND e.OccurredAt >= @Since AND e.Location IS NOT NULL AND e.Location != ''
GROUP BY e.Location, e.EventTypeId, et.Name
HAVING COUNT(*) >= 3;";

        var rows = await conn.QueryAsync<(string Location, string EventTypeName, int Count)>(
            new CommandDefinition(sql, new { ClientId = clientId, Since = now.AddDays(-30) }, cancellationToken: ct));

        return rows.Select(r => new InsightAlert
        {
            ClientId = clientId,
            AlertType = "recurring_location",
            Severity = r.Count >= 5 ? "warning" : "info",
            Title = $"{r.EventTypeName} recurring at {r.Location} ({r.Count} in 30 days)",
            Body = $"There have been {r.Count} \"{r.EventTypeName}\" events at {r.Location} in the last 30 days.",
            GeneratedAt = now,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now,
        }).ToList();
    }
}
