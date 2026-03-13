using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using ImperaOps.Infrastructure.Notifications;
using ImperaOps.Infrastructure.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace ImperaOps.Infrastructure.Tests;

public sealed class WorkflowNotifierTests
{
    private static ImperaOpsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ImperaOpsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ImperaOpsDbContext(opts);
    }

    private static IConfiguration CreateConfig(string baseUrl = "https://app.test.com")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:BaseUrl"] = baseUrl })
            .Build();
    }

    private static (WorkflowNotifier notifier, ImperaOpsDbContext db, INotificationService notifSvc, IBackgroundJobClient jobs, INotificationPushService push)
        CreateSut(ImperaOpsDbContext? db = null)
    {
        db ??= CreateDb();
        var notifSvc = Substitute.For<INotificationService>();
        var jobs = Substitute.For<IBackgroundJobClient>();
        var push = Substitute.For<INotificationPushService>();
        var config = CreateConfig();
        var notifier = new WorkflowNotifier(db, notifSvc, jobs, push, config);
        return (notifier, db, notifSvc, jobs, push);
    }

    [Fact]
    public async Task NotifyUsersAsync_Creates_InApp_Notification_For_Each_Target()
    {
        var db = CreateDb();
        // Add users
        db.Users.Add(new AppUser { Id = 10, Email = "a@test.com", DisplayName = "Alice", PasswordHash = "" });
        db.Users.Add(new AppUser { Id = 20, Email = "b@test.com", DisplayName = "Bob", PasswordHash = "" });
        await db.SaveChangesAsync();

        var (notifier, _, _, _, push) = CreateSut(db);

        await notifier.NotifyUsersAsync(1, "EVT-0001", "SLA Breach", "Event overdue by 2h",
            new long[] { 10, 20 }, null, CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n =>
        {
            Assert.Equal("workflow_rule", n.NotificationType);
            Assert.Equal("Workflow: SLA Breach", n.Title);
            Assert.Equal("Event overdue by 2h", n.Body);
            Assert.Equal("EVT-0001", n.EntityPublicId);
            Assert.Equal(1, n.ClientId);
        });

        // Verify SSE push was sent to both users
        push.Received(1).Push(10, "refresh");
        push.Received(1).Push(20, "refresh");
    }

    [Fact]
    public async Task NotifyUsersAsync_Enqueues_Email_For_Each_Target()
    {
        var db = CreateDb();
        db.Users.Add(new AppUser { Id = 10, Email = "a@test.com", DisplayName = "Alice", PasswordHash = "" });
        await db.SaveChangesAsync();

        var (notifier, _, _, jobs, _) = CreateSut(db);

        await notifier.NotifyUsersAsync(1, "EVT-0001", "Auto-Assign", "Assigned via workflow",
            new long[] { 10 }, null, CancellationToken.None);

        // Verify Hangfire job was enqueued
        jobs.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task NotifyUsersAsync_Respects_InApp_Disabled_Preference()
    {
        var db = CreateDb();
        db.Users.Add(new AppUser { Id = 10, Email = "a@test.com", DisplayName = "Alice", PasswordHash = "" });
        db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = 10,
            NotificationType = "workflow_rule",
            InAppEnabled = false,
            EmailEnabled = true,
        });
        await db.SaveChangesAsync();

        var (notifier, _, _, jobs, push) = CreateSut(db);

        await notifier.NotifyUsersAsync(1, "EVT-0001", "Test Rule", "msg",
            new long[] { 10 }, null, CancellationToken.None);

        // No in-app notification created
        var notifications = await db.Notifications.ToListAsync();
        Assert.Empty(notifications);

        // But email was still enqueued
        jobs.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task NotifyUsersAsync_Respects_Email_Disabled_Preference()
    {
        var db = CreateDb();
        db.Users.Add(new AppUser { Id = 10, Email = "a@test.com", DisplayName = "Alice", PasswordHash = "" });
        db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = 10,
            NotificationType = "workflow_rule",
            InAppEnabled = true,
            EmailEnabled = false,
        });
        await db.SaveChangesAsync();

        var (notifier, _, _, jobs, _) = CreateSut(db);

        await notifier.NotifyUsersAsync(1, "EVT-0001", "Test Rule", "msg",
            new long[] { 10 }, null, CancellationToken.None);

        // In-app notification was created
        var notifications = await db.Notifications.ToListAsync();
        Assert.Single(notifications);

        // No email enqueued
        jobs.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task NotifyUsersAsync_Resolves_Role_Based_Targets()
    {
        var db = CreateDb();
        db.Users.Add(new AppUser { Id = 10, Email = "a@test.com", DisplayName = "Alice", PasswordHash = "" });
        db.Users.Add(new AppUser { Id = 20, Email = "b@test.com", DisplayName = "Bob", PasswordHash = "" });
        db.UserClientAccess.Add(new UserClientAccess { UserId = 10, ClientId = 1, Role = "Manager" });
        db.UserClientAccess.Add(new UserClientAccess { UserId = 20, ClientId = 1, Role = "Admin" });
        await db.SaveChangesAsync();

        var (notifier, _, _, _, _) = CreateSut(db);

        await notifier.NotifyUsersAsync(1, "EVT-0001", "Escalation", "SLA breached",
            null, new[] { "Manager", "Admin" }, CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.UserId == 10);
        Assert.Contains(notifications, n => n.UserId == 20);
    }

    [Fact]
    public async Task NotifyUsersAsync_Deduplicates_UserId_And_Role_Targets()
    {
        var db = CreateDb();
        db.Users.Add(new AppUser { Id = 10, Email = "a@test.com", DisplayName = "Alice", PasswordHash = "" });
        db.UserClientAccess.Add(new UserClientAccess { UserId = 10, ClientId = 1, Role = "Admin" });
        await db.SaveChangesAsync();

        var (notifier, _, _, _, _) = CreateSut(db);

        // User 10 appears in both explicit userIds and role-based resolution
        await notifier.NotifyUsersAsync(1, "EVT-0001", "Rule", "msg",
            new long[] { 10 }, new[] { "Admin" }, CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Single(notifications); // Deduplicated
    }

    [Fact]
    public async Task NotifyUsersAsync_NoOp_When_No_Targets()
    {
        var (notifier, db, _, _, push) = CreateSut();

        await notifier.NotifyUsersAsync(1, "EVT-0001", "Rule", "msg",
            null, null, CancellationToken.None);

        var notifications = await db.Notifications.ToListAsync();
        Assert.Empty(notifications);
        push.DidNotReceive().Push(Arg.Any<long>(), Arg.Any<string>());
    }

    [Fact]
    public async Task NotifyEventAssignedAsync_Delegates_To_NotificationService()
    {
        var (notifier, _, notifSvc, _, _) = CreateSut();

        await notifier.NotifyEventAssignedAsync(10, 1, "EVT-0001", "Test Event", CancellationToken.None);

        await notifSvc.Received(1).NotifyEventAssignedAsync(10, 0, "Workflow Automation", 1, "EVT-0001", "Test Event", Arg.Any<CancellationToken>());
    }
}
