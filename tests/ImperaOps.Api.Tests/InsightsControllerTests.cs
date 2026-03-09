using ImperaOps.Api.Contracts;
using ImperaOps.Api.Controllers;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ImperaOps.Api.Tests;

public sealed class InsightsControllerTests
{
    private const long ClientId = 1;
    private const long ManagerUserId = 10;
    private const long InvestigatorUserId = 20;
    private const long ViewerUserId = 30;

    private async Task<(InsightsController ctrl, Infrastructure.Data.ImperaOpsDbContext db)> CreateController(
        long userId, string role)
    {
        var db = TestHelper.CreateDb();
        await TestHelper.SeedClientAndAccess(db, ClientId, userId, role);
        var ctrl = new InsightsController(db);
        TestHelper.SetUser(ctrl, TestHelper.MakeUser(userId, ClientId, role), db);
        return (ctrl, db);
    }

    private static InsightAlert MakeAlert(long clientId, string alertType, string severity, string title, bool acked = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new InsightAlert
        {
            ClientId = clientId,
            AlertType = alertType,
            Severity = severity,
            Title = title,
            Body = $"Body for {title}",
            IsAcknowledged = acked,
            GeneratedAt = now,
            CreatedAt = now,
        };
    }

    // ── GetAll ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Returns_Alerts_For_Client()
    {
        var (ctrl, db) = await CreateController(InvestigatorUserId, "Investigator");
        db.InsightAlerts.AddRange(
            MakeAlert(ClientId, "spike", "warning", "Alert 1"),
            MakeAlert(ClientId, "spike", "critical", "Alert 2"),
            MakeAlert(999, "spike", "warning", "Other client"));
        await db.SaveChangesAsync();

        var result = await ctrl.GetAll(ClientId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var alerts = Assert.IsAssignableFrom<IEnumerable<InsightAlertDto>>(ok.Value);
        Assert.Equal(2, alerts.Count());
    }

    [Fact]
    public async Task GetAll_Returns_Forbid_For_Viewer()
    {
        var (ctrl, _) = await CreateController(ViewerUserId, "Viewer");
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ctrl.GetAll(ClientId, CancellationToken.None));
    }

    // ── GetSummary ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummary_Returns_Correct_Counts()
    {
        var (ctrl, db) = await CreateController(InvestigatorUserId, "Investigator");
        db.InsightAlerts.AddRange(
            MakeAlert(ClientId, "spike", "critical", "C1"),
            MakeAlert(ClientId, "spike", "critical", "C2"),
            MakeAlert(ClientId, "location_hotspot", "warning", "W1"),
            MakeAlert(ClientId, "recurring_person", "info", "I1"),
            MakeAlert(ClientId, "spike", "warning", "Acked", acked: true));
        await db.SaveChangesAsync();

        var result = await ctrl.GetSummary(ClientId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<InsightSummaryDto>(ok.Value);
        Assert.Equal(4, summary.Total);
        Assert.Equal(2, summary.Critical);
        Assert.Equal(1, summary.Warning);
        Assert.Equal(1, summary.Info);
        Assert.Equal(3, summary.Recent.Count);
    }

    [Fact]
    public async Task GetSummary_With_No_Alerts()
    {
        var (ctrl, _) = await CreateController(InvestigatorUserId, "Investigator");
        var result = await ctrl.GetSummary(ClientId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<InsightSummaryDto>(ok.Value);
        Assert.Equal(0, summary.Total);
        Assert.Empty(summary.Recent);
    }

    // ── Acknowledge ─────────────────────────────────────────────────────

    [Fact]
    public async Task Acknowledge_Sets_Fields()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var alert = MakeAlert(ClientId, "spike", "warning", "Test");
        db.InsightAlerts.Add(alert);
        await db.SaveChangesAsync();

        var result = await ctrl.Acknowledge(alert.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        var updated = await db.InsightAlerts.FirstAsync(a => a.Id == alert.Id);
        Assert.True(updated.IsAcknowledged);
        Assert.Equal(ManagerUserId, updated.AcknowledgedByUserId);
        Assert.NotNull(updated.AcknowledgedAt);
    }

    [Fact]
    public async Task Acknowledge_Returns_Forbid_For_Investigator()
    {
        var (ctrl, db) = await CreateController(InvestigatorUserId, "Investigator");
        var alert = MakeAlert(ClientId, "spike", "warning", "Test");
        db.InsightAlerts.Add(alert);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ctrl.Acknowledge(alert.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Acknowledge_Returns_NotFound_For_Missing_Alert()
    {
        var (ctrl, _) = await CreateController(ManagerUserId, "Manager");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctrl.Acknowledge(999, CancellationToken.None));
    }
}
