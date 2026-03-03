using FreightVis.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreightVis.Infrastructure.Data;

/// <summary>Seeds demo data in development. Safe to run repeatedly — checks before inserting.</summary>
public static class DevSeeder
{
    private static readonly Guid TestClientId   = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChildClientId  = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid TestUserId     = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReporterGuid   = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RegularUserId  = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static async Task SeedAsync(FreightVisDbContext db, ILogger logger)
    {
        // ── Clients ──────────────────────────────────────────────────────────
        if (!await db.Clients.AnyAsync(c => c.Id == TestClientId))
        {
            db.Clients.Add(new Client
            {
                Id        = TestClientId,
                Name      = "Acme Freight Co.",
                IsActive  = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created parent test client");
        }

        if (!await db.Clients.AnyAsync(c => c.Id == ChildClientId))
        {
            db.Clients.Add(new Client
            {
                Id             = ChildClientId,
                Name           = "Acme Midwest Division",
                ParentClientId = TestClientId,
                IsActive       = true,
                CreatedAt      = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created child test client");
        }

        // ── User ─────────────────────────────────────────────────────────────
        if (!await db.Users.AnyAsync(u => u.Id == TestUserId))
        {
            db.Users.Add(new AppUser
            {
                Id           = TestUserId,
                Email        = "admin@freightvis.dev",
                DisplayName  = "Dev Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                IsActive     = true,
                IsSuperAdmin = true,
                CreatedAt    = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created super-admin user  admin@freightvis.dev / password123");
        }
        else
        {
            // Ensure existing dev user is a super admin (idempotent upgrade)
            var existing = await db.Users.FindAsync([TestUserId]);
            if (existing is not null && !existing.IsSuperAdmin)
            {
                existing.IsSuperAdmin = true;
                await db.SaveChangesAsync();
                logger.LogInformation("[DevSeeder] Upgraded test user to super admin");
            }
        }

        // ── Access grant ─────────────────────────────────────────────────────
        if (!await db.UserClientAccess.AnyAsync(a => a.UserId == TestUserId && a.ClientId == TestClientId))
        {
            db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = TestUserId,
                ClientId  = TestClientId,
                Role      = "Admin",
                GrantedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Granted admin access to test client");
        }

        // ── Regular user ─────────────────────────────────────────────────────
        if (!await db.Users.AnyAsync(u => u.Id == RegularUserId))
        {
            db.Users.Add(new AppUser
            {
                Id           = RegularUserId,
                Email        = "user@freightvis.dev",
                DisplayName  = "Dev User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                IsActive     = true,
                IsSuperAdmin = false,
                CreatedAt    = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created regular user  user@freightvis.dev / password123");
        }

        if (!await db.UserClientAccess.AnyAsync(a => a.UserId == RegularUserId && a.ClientId == TestClientId))
        {
            db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = RegularUserId,
                ClientId  = TestClientId,
                Role      = "Member",
                GrantedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Granted member access to test client for regular user");
        }

        // ── Incidents (150 demo rows) ─────────────────────────────────────────
        if (await db.Incidents.AnyAsync(i => i.ClientId == TestClientId))
            return;

        logger.LogInformation("[DevSeeder] Seeding 150 demo incidents...");

        var rng   = new Random(42);
        var types = new[] { 1, 2, 3, 4 };
        var locs  = new[] { "Chicago Hub", "Dallas Depot", "Atlanta Gate", "LAX Terminal", "Newark Port", "Denver Yard" };
        var descs = new[]
        {
            "Cargo inspection delayed due to missing paperwork.",
            "Vehicle breakdown reported on I-90 near mile marker 234.",
            "Temperature excursion in reefer unit — maintenance called.",
            "Driver hours exceeded — mandatory rest initiated.",
            "Customs hold at border crossing, awaiting clearance.",
        };

        var incidents = new List<Incident>();
        for (int i = 0; i < 150; i++)
        {
            var daysAgo     = rng.Next(0, 365);
            var occurredAt  = DateTimeOffset.UtcNow.AddDays(-daysAgo).AddHours(rng.Next(0, 24));
            var type        = types[rng.Next(types.Length)];
            // Bias status: 60% open/in-progress, 30% closed, 10% blocked
            var statusRoll  = rng.Next(100);
            var status      = statusRoll < 35 ? 1   // Open
                            : statusRoll < 60 ? 2   // InProgress
                            : statusRoll < 70 ? 3   // Blocked
                            : 4;                    // Closed

            incidents.Add(new Incident
            {
                Id               = Guid.NewGuid(),
                ClientId         = TestClientId,
                Type             = type,
                Status           = status,
                OccurredAt       = occurredAt,
                Location         = locs[rng.Next(locs.Length)],
                Description      = descs[rng.Next(descs.Length)],
                ReportedByUserId = ReporterGuid,
                OwnerUserId      = rng.Next(2) == 0 ? TestUserId : null,
                CreatedAt        = occurredAt.AddMinutes(rng.Next(5, 60)),
                UpdatedAt        = occurredAt.AddHours(rng.Next(1, 48)),
            });
        }

        db.Incidents.AddRange(incidents);
        await db.SaveChangesAsync();
        logger.LogInformation("[DevSeeder] Seeded {Count} incidents", incidents.Count);
    }
}
