using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

public sealed record SessionDto(
    long Id,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent
);

[ApiController]
[Route("api/v1/sessions")]
public sealed class SessionsController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public SessionsController(ImperaOpsDbContext db) => _db = db;

    /// <summary>Returns all active sessions for the authenticated user.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<SessionDto>>> GetSessions(CancellationToken ct)
    {
        var actorId = CurrentUserId();

        var currentSid = User.FindFirst("sid")?.Value;

        var sessions = await _db.UserTokens
            .AsNoTracking()
            .Where(t => t.UserId == actorId && t.Type == "Session" && t.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new SessionDto(t.Id, t.Description, t.CreatedAt, t.ExpiresAt, t.Token == currentSid))
            .ToListAsync(ct);

        return Ok(sessions);
    }

    /// <summary>Revokes a specific session by ID.</summary>
    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> RevokeSession(long id, CancellationToken ct)
    {
        var actorId = CurrentUserId();

        var session = await _db.UserTokens
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == actorId && t.Type == "Session", ct);

        if (session is null) throw new NotFoundException();

        session.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Revokes all sessions except the current one.</summary>
    [Authorize]
    [HttpDelete("others")]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken ct)
    {
        var actorId = CurrentUserId();

        var currentSid = User.FindFirst("sid")?.Value;

        await _db.UserTokens
            .Where(t => t.UserId == actorId && t.Type == "Session"
                     && (currentSid == null || t.Token != currentSid))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedAt, DateTimeOffset.UtcNow), ct);

        return NoContent();
    }
}
