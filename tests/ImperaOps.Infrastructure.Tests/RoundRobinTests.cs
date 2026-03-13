using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Workflows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace ImperaOps.Infrastructure.Tests;

public class RoundRobinTests
{
    private static ImperaOpsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ImperaOpsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ImperaOpsDbContext(opts);
    }

    [Fact]
    public async Task GetNextRoundRobinUserAsync_RotatesThroughUsers()
    {
        var db = CreateDb();
        var service = new WorkflowActionService(
            db, Substitute.For<IMediator>(), Substitute.For<IAuditService>());

        long ruleId = 42;
        long[] userIds = [10, 20, 30];

        // First call should return index 0 → user 10
        var user1 = await service.GetNextRoundRobinUserAsync(ruleId, userIds, CancellationToken.None);
        Assert.Equal(10, user1);

        // Second call should return index 1 → user 20
        var user2 = await service.GetNextRoundRobinUserAsync(ruleId, userIds, CancellationToken.None);
        Assert.Equal(20, user2);

        // Third call should return index 2 → user 30
        var user3 = await service.GetNextRoundRobinUserAsync(ruleId, userIds, CancellationToken.None);
        Assert.Equal(30, user3);

        // Fourth call should wrap around to index 0 → user 10
        var user4 = await service.GetNextRoundRobinUserAsync(ruleId, userIds, CancellationToken.None);
        Assert.Equal(10, user4);
    }

    [Fact]
    public async Task GetNextRoundRobinUserAsync_IndependentPerRule()
    {
        var db = CreateDb();
        var service = new WorkflowActionService(
            db, Substitute.For<IMediator>(), Substitute.For<IAuditService>());

        long[] userIds = [100, 200];

        // Rule 1 starts at 0
        var r1u1 = await service.GetNextRoundRobinUserAsync(1, userIds, CancellationToken.None);
        Assert.Equal(100, r1u1);

        // Rule 2 also starts at 0 (independent)
        var r2u1 = await service.GetNextRoundRobinUserAsync(2, userIds, CancellationToken.None);
        Assert.Equal(100, r2u1);

        // Rule 1 advances to 1
        var r1u2 = await service.GetNextRoundRobinUserAsync(1, userIds, CancellationToken.None);
        Assert.Equal(200, r1u2);

        // Rule 2 also advances to 1 independently
        var r2u2 = await service.GetNextRoundRobinUserAsync(2, userIds, CancellationToken.None);
        Assert.Equal(200, r2u2);
    }

    [Fact]
    public async Task GetNextRoundRobinUserAsync_SingleUser_AlwaysReturnsSame()
    {
        var db = CreateDb();
        var service = new WorkflowActionService(
            db, Substitute.For<IMediator>(), Substitute.For<IAuditService>());

        long[] userIds = [42];

        var u1 = await service.GetNextRoundRobinUserAsync(1, userIds, CancellationToken.None);
        var u2 = await service.GetNextRoundRobinUserAsync(1, userIds, CancellationToken.None);
        var u3 = await service.GetNextRoundRobinUserAsync(1, userIds, CancellationToken.None);

        Assert.Equal(42, u1);
        Assert.Equal(42, u2);
        Assert.Equal(42, u3);
    }
}
