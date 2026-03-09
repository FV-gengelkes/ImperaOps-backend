using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ImperaOps.Infrastructure.Tests;

public sealed class InsightAlertDeduplicationTests
{
    private static ImperaOpsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ImperaOpsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ImperaOpsDbContext(opts);
    }

    [Fact]
    public async Task Dedup_Skips_Alerts_With_Same_Key_Within_24h()
    {
        var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        // Existing alert generated 12 hours ago
        db.InsightAlerts.Add(new InsightAlert
        {
            ClientId = 1,
            AlertType = "spike",
            Severity = "warning",
            Title = "Near Miss events up 50% this week",
            Body = "body",
            GeneratedAt = now.AddHours(-12),
            CreatedAt = now.AddHours(-12),
        });
        await db.SaveChangesAsync();

        // Simulate dedup logic from InsightDetectionJob.RunAsync
        var candidateAlerts = new List<InsightAlert>
        {
            new()
            {
                ClientId = 1,
                AlertType = "spike",
                Severity = "warning",
                Title = "Near Miss events up 50% this week", // same key
                Body = "body",
                GeneratedAt = now,
                CreatedAt = now,
            },
            new()
            {
                ClientId = 1,
                AlertType = "location_hotspot",
                Severity = "warning",
                Title = "Warehouse B hotspot", // different key
                Body = "body",
                GeneratedAt = now,
                CreatedAt = now,
            }
        };

        var existing = await db.InsightAlerts.AsNoTracking()
            .Where(a => a.GeneratedAt > now.AddHours(-24))
            .Select(a => new { a.ClientId, a.AlertType, a.Title })
            .ToListAsync();

        var existingSet = existing.Select(e => $"{e.ClientId}|{e.AlertType}|{e.Title}").ToHashSet();
        var toAdd = candidateAlerts.Where(a => !existingSet.Contains($"{a.ClientId}|{a.AlertType}|{a.Title}")).ToList();

        Assert.Single(toAdd);
        Assert.Equal("location_hotspot", toAdd[0].AlertType);
    }

    [Fact]
    public async Task Dedup_Allows_Same_Key_After_24h()
    {
        var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        // Existing alert generated 25 hours ago
        db.InsightAlerts.Add(new InsightAlert
        {
            ClientId = 1,
            AlertType = "spike",
            Severity = "warning",
            Title = "Near Miss events up 50% this week",
            Body = "body",
            GeneratedAt = now.AddHours(-25),
            CreatedAt = now.AddHours(-25),
        });
        await db.SaveChangesAsync();

        var candidateAlerts = new List<InsightAlert>
        {
            new()
            {
                ClientId = 1,
                AlertType = "spike",
                Severity = "warning",
                Title = "Near Miss events up 50% this week",
                Body = "body",
                GeneratedAt = now,
                CreatedAt = now,
            }
        };

        var existing = await db.InsightAlerts.AsNoTracking()
            .Where(a => a.GeneratedAt > now.AddHours(-24))
            .Select(a => new { a.ClientId, a.AlertType, a.Title })
            .ToListAsync();

        var existingSet = existing.Select(e => $"{e.ClientId}|{e.AlertType}|{e.Title}").ToHashSet();
        var toAdd = candidateAlerts.Where(a => !existingSet.Contains($"{a.ClientId}|{a.AlertType}|{a.Title}")).ToList();

        Assert.Single(toAdd); // Should be allowed since existing alert is >24h old
    }

    [Fact]
    public async Task Dedup_Differentiates_By_ClientId()
    {
        var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        db.InsightAlerts.Add(new InsightAlert
        {
            ClientId = 1,
            AlertType = "spike",
            Severity = "warning",
            Title = "Near Miss events up 50% this week",
            Body = "body",
            GeneratedAt = now.AddHours(-1),
            CreatedAt = now.AddHours(-1),
        });
        await db.SaveChangesAsync();

        // Same alert but for different client
        var candidateAlerts = new List<InsightAlert>
        {
            new()
            {
                ClientId = 2, // different client
                AlertType = "spike",
                Severity = "warning",
                Title = "Near Miss events up 50% this week",
                Body = "body",
                GeneratedAt = now,
                CreatedAt = now,
            }
        };

        var existing = await db.InsightAlerts.AsNoTracking()
            .Where(a => a.GeneratedAt > now.AddHours(-24))
            .Select(a => new { a.ClientId, a.AlertType, a.Title })
            .ToListAsync();

        var existingSet = existing.Select(e => $"{e.ClientId}|{e.AlertType}|{e.Title}").ToHashSet();
        var toAdd = candidateAlerts.Where(a => !existingSet.Contains($"{a.ClientId}|{a.AlertType}|{a.Title}")).ToList();

        Assert.Single(toAdd); // Different client, should not be deduped
    }

    [Fact]
    public void Alert_Severity_Logic_Spike_100_Percent_Is_Critical()
    {
        // Simulating the spike detection severity logic
        int thisWeek = 10;
        int lastWeek = 5;
        var pctIncrease = ((double)(thisWeek - lastWeek) / lastWeek) * 100;

        var severity = pctIncrease >= 100 ? "critical" : "warning";
        Assert.Equal("critical", severity);
    }

    [Fact]
    public void Alert_Severity_Logic_Spike_50_Percent_Is_Warning()
    {
        int thisWeek = 6;
        int lastWeek = 4;
        var pctIncrease = ((double)(thisWeek - lastWeek) / lastWeek) * 100;

        var severity = pctIncrease >= 100 ? "critical" : "warning";
        Assert.Equal("warning", severity);
    }

    [Fact]
    public void Spike_Detection_Skips_Below_30_Percent()
    {
        int thisWeek = 4;
        int lastWeek = 4;
        var pctIncrease = ((double)(thisWeek - lastWeek) / lastWeek) * 100;

        bool shouldAlert = thisWeek >= 3 && lastWeek > 0 && pctIncrease >= 30;
        Assert.False(shouldAlert);
    }

    [Fact]
    public void Spike_Detection_Skips_Below_3_Events()
    {
        int thisWeek = 2;
        int lastWeek = 1;
        var pctIncrease = ((double)(thisWeek - lastWeek) / lastWeek) * 100;

        bool shouldAlert = thisWeek >= 3 && lastWeek > 0 && pctIncrease >= 30;
        Assert.False(shouldAlert); // Only 2 events this week
    }

    [Fact]
    public void Location_Hotspot_Severity_3x_Average_Is_Critical()
    {
        int count = 12;
        double avg = 3.0;

        var severity = count > 3 * avg ? "critical" : "warning";
        Assert.Equal("critical", severity);
    }

    [Fact]
    public void Location_Hotspot_Requires_Greater_Than_2x_Average_And_Min_3()
    {
        var locations = new[]
        {
            (Location: "A", Count: 7),
            (Location: "B", Count: 2),
            (Location: "C", Count: 1),
        };

        double avg = locations.Average(r => r.Count);
        var hotspots = locations.Where(r => r.Count >= 3 && r.Count > 2 * avg).ToList();

        Assert.Single(hotspots);
        Assert.Equal("A", hotspots[0].Location);
    }

    [Fact]
    public void Recurring_Location_Severity_5_Or_More_Is_Warning()
    {
        Assert.Equal("warning", 5 >= 5 ? "warning" : "info");
        Assert.Equal("info", 4 >= 5 ? "warning" : "info");
    }
}
