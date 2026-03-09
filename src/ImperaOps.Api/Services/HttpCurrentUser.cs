using System.Security.Claims;
using ImperaOps.Application.Abstractions;

namespace ImperaOps.Api.Services;

/// <summary>Resolves the current user from the HTTP context claims.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User
        ?? throw new InvalidOperationException("No active HTTP context.");

    public long Id
    {
        get
        {
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
            return id;
        }
    }

    public string DisplayName =>
        IsSuperAdmin
            ? "ImperaOps Admin"
            : User.FindFirstValue("display_name") ?? "Unknown";

    public bool IsSuperAdmin =>
        User.FindFirstValue("is_super_admin") == "true";

    public bool HasClientAccess(long clientId) =>
        IsSuperAdmin || AuthorizedClientIds().Contains(clientId);

    public HashSet<long> AuthorizedClientIds()
    {
        if (IsSuperAdmin) return [long.MaxValue];
        return User.FindAll("client_id")
                   .Select(c => long.TryParse(c.Value, out var id) ? id : 0L)
                   .Where(id => id > 0)
                   .ToHashSet();
    }
}
