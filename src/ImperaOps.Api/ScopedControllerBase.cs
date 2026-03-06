using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ImperaOps.Api;

public abstract class ScopedControllerBase : ControllerBase
{
    protected bool IsSuperAdmin
        => User.FindFirstValue("is_super_admin") == "true";

    protected HashSet<long> AuthorizedClientIds()
    {
        if (IsSuperAdmin) return [long.MaxValue]; // sentinel — passes all checks
        return User.FindAll("client_id")
                   .Select(c => long.TryParse(c.Value, out var id) ? id : 0L)
                   .Where(id => id > 0)
                   .ToHashSet();
    }

    protected bool HasClientAccess(long clientId)
        => IsSuperAdmin || AuthorizedClientIds().Contains(clientId);

    protected (long? Id, string Name) ResolveActor()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(idStr, out var id);
        var name = IsSuperAdmin
            ? "ImperaOps Admin"
            : User.FindFirstValue("display_name") ?? "Unknown";
        return (id == 0 ? null : id, name);
    }

    protected AuditEvent MakeAudit(
        string entityType, long entityId, long clientId, string eventType, string body)
    {
        var (actorId, actorName) = ResolveActor();
        return new AuditEvent
        {
            ClientId        = clientId,
            EntityType      = entityType,
            EntityId        = entityId,
            EventType       = eventType,
            UserId          = actorId,
            UserDisplayName = actorName,
            Body            = body,
            CreatedAt       = DateTimeOffset.UtcNow,
        };
    }

    protected static async Task<bool> IsAdminOfClientAsync(
        ImperaOpsDbContext db, long clientId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.FindFirstValue("is_super_admin") == "true") return true;
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        if (userId == 0) return false;
        return await db.UserClientAccess
            .AnyAsync(a => a.UserId == userId && a.ClientId == clientId && a.Role == "Admin", ct);
    }

    protected static async Task<string?> GetUserRoleAsync(
        ImperaOpsDbContext db, long clientId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.FindFirstValue("is_super_admin") == "true") return "Admin";
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        if (userId == 0) return null;
        return await db.UserClientAccess
            .Where(a => a.UserId == userId && a.ClientId == clientId)
            .Select(a => a.Role)
            .FirstOrDefaultAsync(ct);
    }

    protected static async Task<bool> IsManagerOrAboveAsync(
        ImperaOpsDbContext db, long clientId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.FindFirstValue("is_super_admin") == "true") return true;
        var role = await GetUserRoleAsync(db, clientId, user, ct);
        return role is "Admin" or "Manager";
    }

    protected static async Task<bool> IsInvestigatorOrAboveAsync(
        ImperaOpsDbContext db, long clientId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.FindFirstValue("is_super_admin") == "true") return true;
        var role = await GetUserRoleAsync(db, clientId, user, ct);
        return role is "Admin" or "Manager" or "Investigator" or "Member";
    }

    protected long CurrentUserId()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
        return id;
    }
}
