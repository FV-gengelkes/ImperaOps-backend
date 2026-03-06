using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record NotificationDto(
    long Id,
    string NotificationType,
    string Title,
    string Body,
    string? EntityPublicId,
    bool IsRead,
    DateTimeOffset CreatedAt
);

public sealed record NotificationPreferenceDto(
    string NotificationType,
    bool EmailEnabled,
    bool InAppEnabled
);

public sealed record SavePreferencesRequest(List<NotificationPreferenceDto> Preferences);

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController : ScopedControllerBase
{
    private static readonly string[] KnownTypes = ["event_assigned", "task_assigned", "comment_added", "status_changed", "task_due_reminder"];

    private readonly ImperaOpsDbContext _db;
    private readonly INotificationPushService _push;

    public NotificationsController(ImperaOpsDbContext db, INotificationPushService push)
    {
        _db   = db;
        _push = push;
    }

    [Authorize]
    [DisableRateLimiting]
    [HttpGet("stream")]
    public async Task StreamAsync(CancellationToken ct)
    {
        var (actorId, _) = ResolveActor();
        if (actorId is null) { Response.StatusCode = 401; return; }

        Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, HttpContext.RequestAborted);

        // Initial ping so the browser knows the connection is live
        await Response.WriteAsync("event: ping\ndata: connected\n\n", cts.Token);
        await Response.Body.FlushAsync(cts.Token);

        // Background heartbeat every 25 s to keep the connection alive through proxies
        var heartbeatTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(25));
            try
            {
                while (await timer.WaitForNextTickAsync(cts.Token))
                {
                    await Response.WriteAsync("event: ping\ndata: heartbeat\n\n", cts.Token);
                    await Response.Body.FlushAsync(cts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        try
        {
            await foreach (var msg in _push.SubscribeAsync(actorId.Value, cts.Token))
            {
                await Response.WriteAsync($"event: notification\ndata: {msg}\n\n", cts.Token);
                await Response.Body.FlushAsync(cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            await cts.CancelAsync();
            await heartbeatTask.ConfigureAwait(false);
        }
    }

    [Authorize]
    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> GetUnreadCount(CancellationToken ct)
    {
        var (actorId, _) = ResolveActor();
        if (actorId is null) return Unauthorized();

        var count = await _db.Notifications
            .CountAsync(n => n.UserId == actorId.Value && !n.IsRead, ct);

        // Count open tasks directly assigned to this user (accurate regardless of notification history)
        var clientIds = await _db.UserClientAccess
            .Where(a => a.UserId == actorId.Value)
            .Select(a => a.ClientId)
            .ToListAsync(ct);

        var taskCount = await _db.Tasks
            .CountAsync(t => clientIds.Contains(t.ClientId) && t.AssignedToUserId == actorId.Value && !t.IsComplete, ct);

        return Ok(new { count, taskCount });
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<object>> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var (actorId, _) = ResolveActor();
        if (actorId is null) return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = _db.Notifications
            .Where(n => n.UserId == actorId.Value)
            .OrderByDescending(n => n.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.NotificationType, n.Title, n.Body, n.EntityPublicId, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [Authorize]
    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        var (actorId, _) = ResolveActor();
        if (actorId is null) return Unauthorized();

        var notification = await _db.Notifications.FindAsync([id], ct);
        if (notification is null || notification.UserId != actorId.Value) return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize]
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var (actorId, _) = ResolveActor();
        if (actorId is null) return Unauthorized();

        await _db.Notifications
            .Where(n => n.UserId == actorId.Value && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return NoContent();
    }

    [Authorize]
    [HttpGet("preferences")]
    public async Task<ActionResult<List<NotificationPreferenceDto>>> GetPreferences(CancellationToken ct)
    {
        var (actorId, _) = ResolveActor();
        if (actorId is null) return Unauthorized();

        var existing = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == actorId.Value)
            .ToDictionaryAsync(p => p.NotificationType, ct);

        var result = KnownTypes.Select(type =>
        {
            if (existing.TryGetValue(type, out var pref))
                return new NotificationPreferenceDto(type, pref.EmailEnabled, pref.InAppEnabled);
            return new NotificationPreferenceDto(type, true, true); // default-on
        }).ToList();

        return Ok(result);
    }

    [Authorize]
    [HttpPut("preferences")]
    public async Task<IActionResult> SavePreferences([FromBody] SavePreferencesRequest req, CancellationToken ct)
    {
        var (actorId, _) = ResolveActor();
        if (actorId is null) return Unauthorized();

        foreach (var dto in req.Preferences)
        {
            if (!KnownTypes.Contains(dto.NotificationType)) continue;

            var existing = await _db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == actorId.Value && p.NotificationType == dto.NotificationType, ct);

            if (existing is not null)
            {
                existing.EmailEnabled = dto.EmailEnabled;
                existing.InAppEnabled = dto.InAppEnabled;
            }
            else
            {
                _db.NotificationPreferences.Add(new Domain.Entities.NotificationPreference
                {
                    UserId           = actorId.Value,
                    NotificationType = dto.NotificationType,
                    EmailEnabled     = dto.EmailEnabled,
                    InAppEnabled     = dto.InAppEnabled,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
