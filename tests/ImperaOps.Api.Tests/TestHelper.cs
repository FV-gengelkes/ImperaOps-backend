using System.Security.Claims;
using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImperaOps.Api.Tests;

internal static class TestHelper
{
    public static ImperaOpsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<ImperaOpsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ImperaOpsDbContext(opts);
    }

    public static ClaimsPrincipal MakeUser(long userId, long clientId, string role, bool isSuperAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("client_id", clientId.ToString()),
            new("display_name", $"User {userId}"),
        };
        if (isSuperAdmin)
            claims.Add(new Claim("is_super_admin", "true"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    public static void SetUser(ControllerBase controller, ClaimsPrincipal user, ImperaOpsDbContext? db = null)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var currentUser = new TestCurrentUser(user);

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(currentUser);
        if (db is not null)
            services.AddSingleton<IAuditService>(new AuditService(db, currentUser));
        else
            services.AddSingleton<IAuditService>(new NullAuditService());

        httpContext.RequestServices = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public static async Task SeedClientAndAccess(ImperaOpsDbContext db, long clientId, long userId, string role)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId))
        {
            db.Clients.Add(new Client
            {
                Id = clientId,
                Name = $"Client {clientId}",
                Slug = $"client-{clientId}",
                Status = "Active",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            db.Users.Add(new AppUser
            {
                Id = userId,
                Email = $"user{userId}@test.com",
                DisplayName = $"User {userId}",
                PasswordHash = "hash",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        db.UserClientAccess.Add(new UserClientAccess
        {
            UserId = userId,
            ClientId = clientId,
            Role = role,
            GrantedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    public static async Task<Event> SeedEvent(ImperaOpsDbContext db, long clientId, string publicId = "EVT-0001")
    {
        var ev = new Event
        {
            ClientId = clientId,
            PublicId = publicId,
            EventTypeId = 1,
            WorkflowStatusId = 1,
            Title = "Test Event",
            Location = "Test Location",
            Description = "Test Description",
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-1),
            ReferenceNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private sealed class TestCurrentUser(ClaimsPrincipal user) : ICurrentUser
    {
        public long Id
        {
            get
            {
                long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
                return id;
            }
        }

        public string DisplayName =>
            IsSuperAdmin ? "ImperaOps Admin" : user.FindFirstValue("display_name") ?? "Unknown";

        public bool IsSuperAdmin =>
            user.FindFirstValue("is_super_admin") == "true";

        public bool HasClientAccess(long clientId) =>
            IsSuperAdmin || AuthorizedClientIds().Contains(clientId);

        public HashSet<long> AuthorizedClientIds()
        {
            if (IsSuperAdmin) return [long.MaxValue];
            return user.FindAll("client_id")
                .Select(c => long.TryParse(c.Value, out var id) ? id : 0L)
                .Where(id => id > 0)
                .ToHashSet();
        }
    }

    private sealed class NullAuditService : IAuditService
    {
        public void Record(string entityType, long entityId, long clientId, string eventType, string body) { }
        public void Record(string entityType, long entityId, long clientId, string eventType, string body, long? actorId, string actorName) { }
    }
}
