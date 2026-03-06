using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Data;

/// <summary>Seeds demo data in development. Safe to run repeatedly — checks before inserting.</summary>
public static class DevSeeder
{
    public static async Task SeedAsync(ImperaOpsDbContext db, string connectionString, ILogger logger)
    {
        var now = DateTimeOffset.UtcNow;

        // ── Clients ──────────────────────────────────────────────────────────
        var parentClient = await db.Clients.FirstOrDefaultAsync(c => c.Name == "Acme Freight Co.");
        if (parentClient is null)
        {
            parentClient = new Client
            {
                Name      = "Acme Freight Co.",
                Slug      = "acme-freight-co",
                IsActive  = true,
                CreatedAt = now,
            };
            db.Clients.Add(parentClient);
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created parent test client (Id={Id})", parentClient.Id);
        }

        var childClient = await db.Clients.FirstOrDefaultAsync(c => c.Name == "Acme Midwest Division");
        if (childClient is null)
        {
            childClient = new Client
            {
                Name           = "Acme Midwest Division",
                Slug           = "acme-midwest-division",
                ParentClientId = parentClient.Id,
                IsActive       = true,
                CreatedAt      = now,
            };
            db.Clients.Add(childClient);
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created child test client (Id={Id})", childClient.Id);
        }

        var testClientId = parentClient.Id;

        // ── Workflow Statuses (system, ClientId=0) ────────────────────────────
        var statusNames = new[] { ("Open", "#3B82F6", false, 1), ("In Progress", "#F59E0B", false, 2), ("Blocked", "#EF4444", false, 3), ("Closed", "#16A34A", true, 4) };
        var statusMap   = new Dictionary<string, long>();

        foreach (var (name, color, isClosed, sortOrder) in statusNames)
        {
            var existing = await db.WorkflowStatuses.FirstOrDefaultAsync(s => s.ClientId == 0 && s.Name == name);
            if (existing is null)
            {
                existing = new WorkflowStatus
                {
                    ClientId  = 0,
                    Name      = name,
                    Color     = color,
                    IsClosed  = isClosed,
                    SortOrder = sortOrder,
                    IsSystem  = true,
                    IsActive  = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.WorkflowStatuses.Add(existing);
                await db.SaveChangesAsync();
            }
            statusMap[name] = existing.Id;
        }

        // ── Event Types (system, ClientId=0) ──────────────────────────────────
        var typeNames = new[] { "Accident", "Injury", "Near Miss", "Property Damage", "Safety Violation" };
        var typeMap   = new Dictionary<string, long>();

        for (int i = 0; i < typeNames.Length; i++)
        {
            var name     = typeNames[i];
            var existing = await db.EventTypes.FirstOrDefaultAsync(t => t.ClientId == 0 && t.Name == name);
            if (existing is null)
            {
                existing = new EventType
                {
                    ClientId  = 0,
                    Name      = name,
                    SortOrder = i + 1,
                    IsSystem  = true,
                    IsActive  = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.EventTypes.Add(existing);
                await db.SaveChangesAsync();
            }
            typeMap[name] = existing.Id;
        }

        // ── Workflow Transitions (system) ─────────────────────────────────────
        if (!await db.WorkflowTransitions.AnyAsync(t => t.ClientId == 0))
        {
            // Allow any → any transition for system defaults
            foreach (var (_, toId) in statusMap)
            {
                db.WorkflowTransitions.Add(new WorkflowTransition
                {
                    ClientId     = 0,
                    FromStatusId = null,
                    ToStatusId   = toId,
                    EventTypeId  = null,
                    IsDefault    = true,
                    CreatedAt    = now,
                });
            }
            await db.SaveChangesAsync();
        }

        // ── Users ─────────────────────────────────────────────────────────────
        AppUser? adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@imperaops.dev");
        if (adminUser is null)
        {
            adminUser = new AppUser
            {
                Email        = "admin@imperaops.dev",
                DisplayName  = "Dev Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                IsActive     = true,
                IsSuperAdmin = true,
                CreatedAt    = now,
            };
            db.Users.Add(adminUser);
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created super-admin user  admin@imperaops.dev / password123");
        }
        else if (!adminUser.IsSuperAdmin)
        {
            adminUser.IsSuperAdmin = true;
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Upgraded test user to super admin");
        }

        AppUser? regularUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "user@imperaops.dev");
        if (regularUser is null)
        {
            regularUser = new AppUser
            {
                Email        = "user@imperaops.dev",
                DisplayName  = "Dev User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                IsActive     = true,
                IsSuperAdmin = false,
                CreatedAt    = now,
            };
            db.Users.Add(regularUser);
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Created regular user  user@imperaops.dev / password123");
        }

        // ── Access grants ─────────────────────────────────────────────────────
        if (!await db.UserClientAccess.AnyAsync(a => a.UserId == adminUser.Id && a.ClientId == testClientId))
        {
            db.UserClientAccess.Add(new UserClientAccess { UserId = adminUser.Id, ClientId = testClientId, Role = "Admin", GrantedAt = now });
            await db.SaveChangesAsync();
        }

        if (!await db.UserClientAccess.AnyAsync(a => a.UserId == regularUser.Id && a.ClientId == testClientId))
        {
            db.UserClientAccess.Add(new UserClientAccess { UserId = regularUser.Id, ClientId = testClientId, Role = "Member", GrantedAt = now });
            await db.SaveChangesAsync();
        }

        // ── Events (150 demo rows) ─────────────────────────────────────────────
        if (await db.Events.AnyAsync(e => e.ClientId == testClientId))
            return;

        logger.LogInformation("[DevSeeder] Seeding 150 demo events...");

        var counter = new CounterService(connectionString);
        var rng     = new Random(42);

        var typeIds  = typeMap.Values.ToArray();
        var openId   = statusMap["Open"];
        var inProgId = statusMap["In Progress"];
        var blockedId= statusMap["Blocked"];
        var closedId = statusMap["Closed"];

        var locs  = new[] { "Chicago Hub", "Dallas Depot", "Atlanta Gate", "LAX Terminal", "Newark Port", "Denver Yard" };
        var descs = new[]
        {
            "Cargo inspection delayed due to missing paperwork.",
            "Vehicle breakdown reported on I-90 near mile marker 234.",
            "Temperature excursion in reefer unit — maintenance called.",
            "Driver hours exceeded — mandatory rest initiated.",
            "Customs hold at border crossing, awaiting clearance.",
        };
        var titles = new[]
        {
            "Freight inspection delay",
            "Vehicle breakdown on I-90",
            "Reefer unit temperature excursion",
            "Driver hours violation",
            "Customs hold at border crossing",
        };

        var events = new List<Event>();
        for (int i = 0; i < 150; i++)
        {
            var daysAgo    = rng.Next(0, 365);
            var occurredAt = DateTimeOffset.UtcNow.AddDays(-daysAgo).AddHours(rng.Next(0, 24));
            var typeId     = typeIds[rng.Next(typeIds.Length)];
            var statusRoll = rng.Next(100);
            var statusId   = statusRoll < 35 ? openId
                           : statusRoll < 60 ? inProgId
                           : statusRoll < 70 ? blockedId
                           : closedId;

            var refNumber = await counter.AllocateAsync(testClientId, "event", default);
            var publicId  = $"EVT-{refNumber:D4}";

            events.Add(new Event
            {
                ClientId         = testClientId,
                PublicId         = publicId,
                EventTypeId      = typeId,
                WorkflowStatusId = statusId,
                Title            = titles[rng.Next(titles.Length)],
                OccurredAt       = occurredAt,
                Location         = locs[rng.Next(locs.Length)],
                Description      = descs[rng.Next(descs.Length)],
                ReportedByUserId = adminUser.Id,
                OwnerUserId      = rng.Next(2) == 0 ? adminUser.Id : null,
                ReferenceNumber  = refNumber,
                CreatedAt        = occurredAt.AddMinutes(rng.Next(5, 60)),
                UpdatedAt        = occurredAt.AddHours(rng.Next(1, 48)),
            });
        }

        db.Events.AddRange(events);
        await db.SaveChangesAsync();
        logger.LogInformation("[DevSeeder] Seeded {Count} events", events.Count);
    }
}
