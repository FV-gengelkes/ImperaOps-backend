using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api;

/// <summary>
/// Rejects requests whose JWT contains a revoked or expired session ID.
/// Only runs when the token includes a "sid" claim (i.e. issued after session tracking was introduced).
/// </summary>
public sealed class SessionValidationFilter : IAsyncActionFilter
{
    private readonly ImperaOpsDbContext _db;

    public SessionValidationFilter(ImperaOpsDbContext db) => _db = db;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var sid = user.FindFirst("sid")?.Value;
            if (sid is not null)
            {
                var valid = await _db.UserTokens
                    .AsNoTracking()
                    .AnyAsync(t => t.Token == sid && t.Type == "Session" && t.ExpiresAt > DateTimeOffset.UtcNow);

                if (!valid)
                {
                    context.Result = new UnauthorizedObjectResult(new { message = "Session expired or revoked. Please log in again." });
                    return;
                }
            }
        }

        await next();
    }
}
