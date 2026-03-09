using ImperaOps.Api.Contracts;
using ImperaOps.Api.Controllers;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ImperaOps.Api.Tests;

public sealed class EventLinksControllerTests
{
    private const long ClientId = 1;
    private const long ManagerUserId = 10;
    private const long InvestigatorUserId = 20;
    private const long AdminUserId = 40;

    private async Task<(EventLinksController ctrl, Infrastructure.Data.ImperaOpsDbContext db)> CreateController(
        long userId, string role)
    {
        var db = TestHelper.CreateDb();
        await TestHelper.SeedClientAndAccess(db, ClientId, userId, role);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Server=localhost;Database=test"
            })
            .Build();

        var ctrl = new EventLinksController(db, config);
        TestHelper.SetUser(ctrl, TestHelper.MakeUser(userId, ClientId, role), db);
        return (ctrl, db);
    }

    // ── CreateGroup ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGroup_Creates_Group_For_Manager()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");

        var result = await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Equipment Failures", "Conveyor issues", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        var group = await db.EventLinkGroups.FirstOrDefaultAsync();
        Assert.NotNull(group);
        Assert.Equal("Equipment Failures", group.Title);
        Assert.Equal("Conveyor issues", group.Description);
        Assert.Equal(ClientId, group.ClientId);
    }

    [Fact]
    public async Task CreateGroup_With_EventIds_Creates_Links()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev1 = await TestHelper.SeedEvent(db, ClientId, "EVT-0001");
        var ev2 = await TestHelper.SeedEvent(db, ClientId, "EVT-0002");

        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Group", null, [ev1.Id, ev2.Id]),
            CancellationToken.None);

        var links = await db.EventLinks.ToListAsync();
        Assert.Equal(2, links.Count);
    }

    [Fact]
    public async Task CreateGroup_Returns_Forbid_For_Investigator()
    {
        var (ctrl, _) = await CreateController(InvestigatorUserId, "Investigator");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ctrl.CreateGroup(
                new CreateEventLinkGroupRequest(ClientId, "Group", null, null),
                CancellationToken.None));
    }

    // ── UpdateGroup ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateGroup_Updates_Title_And_Description()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Old Title", "Old Desc", null),
            CancellationToken.None);
        var group = await db.EventLinkGroups.FirstAsync();

        var result = await ctrl.UpdateGroup(group.Id,
            new UpdateEventLinkGroupRequest("New Title", "New Desc"),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await db.Entry(group).ReloadAsync();
        Assert.Equal("New Title", group.Title);
        Assert.Equal("New Desc", group.Description);
    }

    [Fact]
    public async Task UpdateGroup_Returns_NotFound_For_Missing_Group()
    {
        var (ctrl, _) = await CreateController(ManagerUserId, "Manager");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctrl.UpdateGroup(999,
                new UpdateEventLinkGroupRequest("Title", null),
                CancellationToken.None));
    }

    // ── DeleteGroup ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteGroup_Soft_Deletes_Group_And_Links()
    {
        var (ctrl, db) = await CreateController(AdminUserId, "Admin");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Group", null, [ev.Id]),
            CancellationToken.None);
        var group = await db.EventLinkGroups.FirstAsync();

        var result = await ctrl.DeleteGroup(group.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        var found = await db.EventLinkGroups.FirstOrDefaultAsync(g => g.Id == group.Id);
        Assert.Null(found);

        var raw = await db.EventLinkGroups.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == group.Id);
        Assert.NotNull(raw);
        Assert.NotNull(raw.DeletedAt);
    }

    [Fact]
    public async Task DeleteGroup_Returns_Forbid_For_Manager()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Group", null, null),
            CancellationToken.None);
        var group = await db.EventLinkGroups.FirstAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ctrl.DeleteGroup(group.Id, CancellationToken.None));
    }

    // ── AddEventToGroup ─────────────────────────────────────────────────

    [Fact]
    public async Task AddEventToGroup_Adds_Link()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Group", null, null),
            CancellationToken.None);
        var group = await db.EventLinkGroups.FirstAsync();

        var result = await ctrl.AddEventToGroup(group.Id,
            new AddEventToGroupRequest(ev.Id), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var link = await db.EventLinks.FirstOrDefaultAsync(l => l.LinkGroupId == group.Id && l.EventId == ev.Id);
        Assert.NotNull(link);
    }

    [Fact]
    public async Task AddEventToGroup_Returns_Conflict_For_Duplicate()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Group", null, [ev.Id]),
            CancellationToken.None);
        var group = await db.EventLinkGroups.FirstAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            ctrl.AddEventToGroup(group.Id,
                new AddEventToGroupRequest(ev.Id), CancellationToken.None));
    }

    // ── RemoveEventFromGroup ────────────────────────────────────────────

    [Fact]
    public async Task RemoveEventFromGroup_Soft_Deletes_Link()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Group", null, [ev.Id]),
            CancellationToken.None);
        var group = await db.EventLinkGroups.FirstAsync();

        var result = await ctrl.RemoveEventFromGroup(group.Id, ev.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        var link = await db.EventLinks.FirstOrDefaultAsync(l => l.LinkGroupId == group.Id && l.EventId == ev.Id);
        Assert.Null(link);
    }

    [Fact]
    public async Task RemoveEventFromGroup_Returns_NotFound_For_Missing_Link()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        await ctrl.CreateGroup(
            new CreateEventLinkGroupRequest(ClientId, "Group", null, null),
            CancellationToken.None);
        var group = await db.EventLinkGroups.FirstAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctrl.RemoveEventFromGroup(group.Id, 999, CancellationToken.None));
    }
}
