using ImperaOps.Api.Contracts;
using ImperaOps.Api.Controllers;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ImperaOps.Api.Tests;

public sealed class InvestigationsControllerTests
{
    private const long ClientId = 1;
    private const long ManagerUserId = 10;
    private const long InvestigatorUserId = 20;
    private const long ViewerUserId = 30;

    private async Task<(InvestigationsController ctrl, Infrastructure.Data.ImperaOpsDbContext db)> CreateController(
        long userId, string role)
    {
        var db = TestHelper.CreateDb();
        await TestHelper.SeedClientAndAccess(db, ClientId, userId, role);
        var ctrl = new InvestigationsController(db);
        TestHelper.SetUser(ctrl, TestHelper.MakeUser(userId, ClientId, role), db);
        return (ctrl, db);
    }

    // ── Start Investigation ─────────────────────────────────────────────

    [Fact]
    public async Task Start_Returns_NotFound_When_Event_Missing()
    {
        var (ctrl, _) = await CreateController(ManagerUserId, "Manager");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctrl.Start("EVT-9999", new CreateInvestigationRequest(null), 0, CancellationToken.None));
    }

    [Fact]
    public async Task Start_Creates_Investigation_For_Manager()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        var result = await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(ManagerUserId), 0, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        var inv = await db.Investigations.FirstOrDefaultAsync(i => i.EventId == ev.Id);
        Assert.NotNull(inv);
        Assert.Equal("draft", inv.Status);
        Assert.Equal(ManagerUserId, inv.LeadInvestigatorUserId);
    }

    [Fact]
    public async Task Start_Returns_Forbid_For_Investigator()
    {
        var (ctrl, db) = await CreateController(InvestigatorUserId, "Investigator");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None));
    }

    [Fact]
    public async Task Start_Returns_Conflict_When_Investigation_Already_Exists()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() =>
            ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None));
    }

    // ── Get Investigation ───────────────────────────────────────────────

    [Fact]
    public async Task Get_Returns_Null_When_No_Investigation()
    {
        var (ctrl, db) = await CreateController(InvestigatorUserId, "Investigator");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        var result = await ctrl.Get(ev.PublicId, 0, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Null(ok.Value);
    }

    [Fact]
    public async Task Get_Returns_Investigation_Dto()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(ManagerUserId), 0, CancellationToken.None);

        // Re-create controller as investigator to test read access
        var invCtrl = new InvestigationsController(db);
        await TestHelper.SeedClientAndAccess(db, ClientId, InvestigatorUserId, "Investigator");
        TestHelper.SetUser(invCtrl, TestHelper.MakeUser(InvestigatorUserId, ClientId, "Investigator"), db);

        var result = await invCtrl.Get(ev.PublicId, 0, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<InvestigationDto>(ok.Value);
        Assert.Equal("draft", dto.Status);
    }

    // ── Update / Status Transitions ─────────────────────────────────────

    [Fact]
    public async Task Update_Valid_Transition_Draft_To_InProgress()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(ManagerUserId), 0, CancellationToken.None);

        var result = await ctrl.Update(ev.PublicId,
            new UpdateInvestigationRequest("in_progress", null, null, null, null), 0, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var inv = await db.Investigations.FirstAsync(i => i.EventId == ev.Id);
        Assert.Equal("in_progress", inv.Status);
        Assert.NotNull(inv.StartedAt);
    }

    [Fact]
    public async Task Update_Valid_Transition_InProgress_To_Review()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("in_progress", null, null, null, null), 0, CancellationToken.None);

        var result = await ctrl.Update(ev.PublicId,
            new UpdateInvestigationRequest("review", null, null, null, null), 0, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var inv = await db.Investigations.FirstAsync(i => i.EventId == ev.Id);
        Assert.Equal("review", inv.Status);
    }

    [Fact]
    public async Task Update_Valid_Transition_Review_To_Completed()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("in_progress", null, null, null, null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("review", null, null, null, null), 0, CancellationToken.None);

        var result = await ctrl.Update(ev.PublicId,
            new UpdateInvestigationRequest("completed", null, null, null, null), 0, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var inv = await db.Investigations.FirstAsync(i => i.EventId == ev.Id);
        Assert.Equal("completed", inv.Status);
        Assert.NotNull(inv.CompletedAt);
    }

    [Fact]
    public async Task Update_Valid_Reopen_Completed_To_InProgress()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("in_progress", null, null, null, null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("review", null, null, null, null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("completed", null, null, null, null), 0, CancellationToken.None);

        var result = await ctrl.Update(ev.PublicId,
            new UpdateInvestigationRequest("in_progress", null, null, null, null), 0, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_Invalid_Transition_Draft_To_Review_Throws_Validation()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            ctrl.Update(ev.PublicId,
                new UpdateInvestigationRequest("review", null, null, null, null), 0, CancellationToken.None));
    }

    [Fact]
    public async Task Update_Invalid_Transition_Draft_To_Completed_Throws_Validation()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            ctrl.Update(ev.PublicId,
                new UpdateInvestigationRequest("completed", null, null, null, null), 0, CancellationToken.None));
    }

    [Fact]
    public async Task Update_Sets_Summary_And_RCA()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);

        await ctrl.Update(ev.PublicId,
            new UpdateInvestigationRequest(null, "Test summary", "Root cause", "Fix it", null), 0, CancellationToken.None);

        var inv = await db.Investigations.FirstAsync(i => i.EventId == ev.Id);
        Assert.Equal("Test summary", inv.Summary);
        Assert.Equal("Root cause", inv.RootCauseAnalysis);
        Assert.Equal("Fix it", inv.CorrectiveActions);
    }

    [Fact]
    public async Task Update_Investigator_Can_Only_Update_Own_Investigation()
    {
        var db = TestHelper.CreateDb();
        await TestHelper.SeedClientAndAccess(db, ClientId, ManagerUserId, "Manager");
        await TestHelper.SeedClientAndAccess(db, ClientId, InvestigatorUserId, "Investigator");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        // Manager starts with investigator as lead
        var mgrCtrl = new InvestigationsController(db);
        TestHelper.SetUser(mgrCtrl, TestHelper.MakeUser(ManagerUserId, ClientId, "Manager"), db);
        await mgrCtrl.Start(ev.PublicId, new CreateInvestigationRequest(InvestigatorUserId), 0, CancellationToken.None);

        // Investigator (lead) can update
        var invCtrl = new InvestigationsController(db);
        TestHelper.SetUser(invCtrl, TestHelper.MakeUser(InvestigatorUserId, ClientId, "Investigator"), db);
        var result = await invCtrl.Update(ev.PublicId,
            new UpdateInvestigationRequest("in_progress", null, null, null, null), 0, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        // Different investigator cannot update
        var otherUserId = 99L;
        await TestHelper.SeedClientAndAccess(db, ClientId, otherUserId, "Investigator");
        var otherCtrl = new InvestigationsController(db);
        TestHelper.SetUser(otherCtrl, TestHelper.MakeUser(otherUserId, ClientId, "Investigator"), db);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            otherCtrl.Update(ev.PublicId,
                new UpdateInvestigationRequest(null, "hacked", null, null, null), 0, CancellationToken.None));
    }

    // ── Witnesses ───────────────────────────────────────────────────────

    [Fact]
    public async Task AddWitness_And_GetWitnesses()
    {
        var (ctrl, db) = await CreateController(InvestigatorUserId, "Investigator");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        // Need manager to start investigation
        await TestHelper.SeedClientAndAccess(db, ClientId, ManagerUserId, "Manager");
        var mgrCtrl = new InvestigationsController(db);
        TestHelper.SetUser(mgrCtrl, TestHelper.MakeUser(ManagerUserId, ClientId, "Manager"), db);
        await mgrCtrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);

        var addResult = await ctrl.AddWitness(ev.PublicId,
            new CreateWitnessRequest("John Doe", "john@test.com", "I saw everything", null), 0, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(addResult);
        var dto = Assert.IsType<WitnessDto>(ok.Value);
        Assert.Equal("John Doe", dto.WitnessName);
        Assert.Equal(0, dto.SortOrder);

        var getResult = await ctrl.GetWitnesses(ev.PublicId, 0, CancellationToken.None);
        var getOk = Assert.IsType<OkObjectResult>(getResult);
        var witnesses = Assert.IsAssignableFrom<IEnumerable<WitnessDto>>(getOk.Value);
        Assert.Single(witnesses);
    }

    [Fact]
    public async Task DeleteWitness_Requires_Manager()
    {
        var db = TestHelper.CreateDb();
        await TestHelper.SeedClientAndAccess(db, ClientId, ManagerUserId, "Manager");
        await TestHelper.SeedClientAndAccess(db, ClientId, InvestigatorUserId, "Investigator");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        var mgrCtrl = new InvestigationsController(db);
        TestHelper.SetUser(mgrCtrl, TestHelper.MakeUser(ManagerUserId, ClientId, "Manager"), db);
        await mgrCtrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);

        var invCtrl = new InvestigationsController(db);
        TestHelper.SetUser(invCtrl, TestHelper.MakeUser(InvestigatorUserId, ClientId, "Investigator"), db);
        var addResult = await invCtrl.AddWitness(ev.PublicId,
            new CreateWitnessRequest("Jane", null, "Statement", null), 0, CancellationToken.None);
        var witnessDto = Assert.IsType<WitnessDto>(((OkObjectResult)addResult).Value);

        // Investigator cannot delete
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            invCtrl.DeleteWitness(ev.PublicId, witnessDto.Id, 0, CancellationToken.None));

        // Manager can delete
        var mgrDelete = await mgrCtrl.DeleteWitness(ev.PublicId, witnessDto.Id, 0, CancellationToken.None);
        Assert.IsType<NoContentResult>(mgrDelete);
    }

    // ── Evidence ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddEvidence_And_GetEvidence()
    {
        var (ctrl, db) = await CreateController(InvestigatorUserId, "Investigator");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        await TestHelper.SeedClientAndAccess(db, ClientId, ManagerUserId, "Manager");
        var mgrCtrl = new InvestigationsController(db);
        TestHelper.SetUser(mgrCtrl, TestHelper.MakeUser(ManagerUserId, ClientId, "Manager"), db);
        await mgrCtrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);

        var addResult = await ctrl.AddEvidence(ev.PublicId,
            new CreateEvidenceRequest("Security Camera", "Footage from entrance", "video", null, null), 0, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(addResult);
        var dto = Assert.IsType<EvidenceDto>(ok.Value);
        Assert.Equal("Security Camera", dto.Title);
        Assert.Equal("video", dto.EvidenceType);

        var getResult = await ctrl.GetEvidence(ev.PublicId, 0, CancellationToken.None);
        var getOk = Assert.IsType<OkObjectResult>(getResult);
        var evidence = Assert.IsAssignableFrom<IEnumerable<EvidenceDto>>(getOk.Value);
        Assert.Single(evidence);
    }

    // ── Audit Events ────────────────────────────────────────────────────

    [Fact]
    public async Task Start_Creates_Audit_Event()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);

        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);

        var audits = await db.AuditEvents.ToListAsync();
        Assert.Contains(audits, a => a.EventType == "investigation_started");
    }

    [Fact]
    public async Task Completing_Investigation_Creates_Completed_Audit()
    {
        var (ctrl, db) = await CreateController(ManagerUserId, "Manager");
        var ev = await TestHelper.SeedEvent(db, ClientId);
        await ctrl.Start(ev.PublicId, new CreateInvestigationRequest(null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("in_progress", null, null, null, null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("review", null, null, null, null), 0, CancellationToken.None);
        await ctrl.Update(ev.PublicId, new UpdateInvestigationRequest("completed", null, null, null, null), 0, CancellationToken.None);

        var audits = await db.AuditEvents.ToListAsync();
        Assert.Contains(audits, a => a.EventType == "investigation_completed");
    }
}
