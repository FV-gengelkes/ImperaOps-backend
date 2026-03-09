using ImperaOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Data;

/// <summary>Seeds system defaults and a super-admin user in development. Safe to run repeatedly.</summary>
public static class DevSeeder
{
    public static async Task SeedAsync(ImperaOpsDbContext db, string connectionString, ILogger logger)
    {
        var now = DateTimeOffset.UtcNow;

        // ── Workflow Statuses (system, ClientId=0) ────────────────────────────
        var statusNames = new[] { ("Open", "#3B82F6", false, 1), ("In Progress", "#F59E0B", false, 2), ("Blocked", "#EF4444", false, 3), ("Closed", "#16A34A", true, 4) };

        foreach (var (name, color, isClosed, sortOrder) in statusNames)
        {
            if (!await db.WorkflowStatuses.AnyAsync(s => s.ClientId == 0 && s.Name == name))
            {
                db.WorkflowStatuses.Add(new WorkflowStatus
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
                });
                await db.SaveChangesAsync();
            }
        }

        // ── Remove legacy system event types (now template-defined) ──────────
        var legacyTypes = await db.EventTypes
            .IgnoreQueryFilters()
            .Where(t => t.ClientId == 0 && t.IsSystem && t.DeletedAt == null)
            .ToListAsync();
        if (legacyTypes.Count > 0)
        {
            foreach (var lt in legacyTypes) lt.DeletedAt = now;
            await db.SaveChangesAsync();
            logger.LogInformation("[DevSeeder] Soft-deleted {Count} legacy system event types", legacyTypes.Count);
        }

        // ── Workflow Transitions (system) ─────────────────────────────────────
        if (!await db.WorkflowTransitions.AnyAsync(t => t.ClientId == 0))
        {
            var statusMap = await db.WorkflowStatuses
                .Where(s => s.ClientId == 0)
                .ToDictionaryAsync(s => s.Name, s => s.Id);

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

        // ── Super-admin user ─────────────────────────────────────────────────
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@imperaops.dev");
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
    }
}
