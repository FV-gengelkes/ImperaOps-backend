using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace ImperaOps.Api;

public abstract class ScopedControllerBase : ControllerBase
{
    // ── Resolved from DI via HttpContext ──────────────────────────────────

    protected ICurrentUser CurrentUser =>
        HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

    protected IAuditService Audit =>
        HttpContext.RequestServices.GetRequiredService<IAuditService>();

    // ── Convenience wrappers (delegate to ICurrentUser) ──────────────────

    protected bool IsSuperAdmin => CurrentUser.IsSuperAdmin;

    protected long CurrentUserId() => CurrentUser.Id;

    protected bool HasClientAccess(long clientId) => CurrentUser.HasClientAccess(clientId);

    protected HashSet<long> AuthorizedClientIds() => CurrentUser.AuthorizedClientIds();

    /// <summary>Throws NotFoundException if the user has no access to this client.</summary>
    protected void RequireClientAccess(long clientId)
    {
        if (!CurrentUser.HasClientAccess(clientId))
            throw new NotFoundException();
    }

    // ── Module gating ──────────────────────────────────────────────────

    /// <summary>Throws ForbiddenException if the given module is not enabled for this client.</summary>
    protected static async Task RequireModuleAsync(
        ImperaOpsDbContext db, long clientId, string moduleId, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => c.EnabledModuleIds)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(client))
            throw new ForbiddenException("This feature requires an add-on module that is not enabled.");

        List<string>? ids;
        try { ids = JsonSerializer.Deserialize<List<string>>(client); }
        catch { ids = null; }

        if (ids == null || !ids.Contains(moduleId))
            throw new ForbiddenException("This feature requires an add-on module that is not enabled.");
    }

    // ── Role queries ─────────────────────────────────────────────────────

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

    protected static async Task<bool> IsAdminOfClientAsync(
        ImperaOpsDbContext db, long clientId, ClaimsPrincipal user, CancellationToken ct)
    {
        var role = await GetUserRoleAsync(db, clientId, user, ct);
        return role is "Admin";
    }

    protected static async Task<bool> IsManagerOrAboveAsync(
        ImperaOpsDbContext db, long clientId, ClaimsPrincipal user, CancellationToken ct)
    {
        var role = await GetUserRoleAsync(db, clientId, user, ct);
        return role is "Admin" or "Manager";
    }

    protected static async Task<bool> IsInvestigatorOrAboveAsync(
        ImperaOpsDbContext db, long clientId, ClaimsPrincipal user, CancellationToken ct)
    {
        var role = await GetUserRoleAsync(db, clientId, user, ct);
        return role is "Admin" or "Manager" or "Investigator" or "Member";
    }

    // ── Convenience: resolve actor tuple (used by some controllers) ──────

    protected (long? Id, string Name) ResolveActor()
    {
        var user = CurrentUser;
        return (user.Id == 0 ? null : user.Id, user.DisplayName);
    }
}
