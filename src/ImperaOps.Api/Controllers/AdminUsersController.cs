using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "SuperAdmin")]
public sealed class AdminUsersController(
    ImperaOpsDbContext db,
    IEmailService email,
    IConfiguration config) : ScopedControllerBase
{
    // ── Users — list & create ────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().ToListAsync(ct);

        var clientCounts = await db.UserClientAccess
            .AsNoTracking()
            .GroupBy(a => a.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return Ok(users
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserDto(
                u.Id, u.Email, u.DisplayName, u.IsActive, u.IsSuperAdmin,
                clientCounts.TryGetValue(u.Id, out var cnt) ? cnt : 0,
                u.CreatedAt,
                u.IsTotpEnabled))
            .ToList());
    }

    [HttpPost("users")]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<AdminUserDto>> CreateUser(
        [FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))       throw new ValidationException("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) throw new ValidationException("Display name is required.");

        var emailAddr = req.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == emailAddr, ct))
            throw new ConflictException("A user with that email already exists.");

        var user = new AppUser
        {
            Email        = emailAddr,
            DisplayName  = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
            IsActive     = true,
            IsSuperAdmin = req.IsSuperAdmin,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        if (req.ClientId.HasValue)
        {
            var client = await db.Clients.FindAsync([req.ClientId.Value], ct);
            if (client is null) throw new ValidationException("Client not found.");

            db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = user.Id,
                ClientId  = req.ClientId.Value,
                Role      = req.Role ?? "Member",
                GrantedAt = DateTimeOffset.UtcNow,
            });
        }

        var auditClientId = req.ClientId ?? req.AuditClientId ?? 0;
        var inviteToken = GenerateToken();
        db.UserTokens.Add(new UserToken
        {
            UserId    = user.Id,
            Token     = inviteToken,
            Type      = "Invite",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        Audit.Record("user", user.Id, auditClientId, "created",
            $"User \"{user.Email}\" created{(user.IsSuperAdmin ? " as super admin" : "")}.");

        await db.SaveChangesAsync(ct);

        var baseUrl   = config["App:BaseUrl"] ?? "http://localhost:3000";
        var inviteUrl = $"{baseUrl}/set-password?token={inviteToken}";
        var emailSent = false;
        try
        {
            await email.SendInviteAsync(user.Email, user.DisplayName, inviteUrl, ct);
            emailSent = true;
        }
        catch { /* non-fatal — invite URL is returned so admin can share it manually */ }

        var dto = new AdminUserDto(user.Id, user.Email, user.DisplayName,
            user.IsActive, user.IsSuperAdmin, req.ClientId.HasValue ? 1 : 0, user.CreatedAt, user.IsTotpEnabled);
        return CreatedAtAction(nameof(GetUsers), new InviteUserResult<AdminUserDto>(dto, inviteUrl, emailSent));
    }

    // ── Users — update & toggle ──────────────────────────────────────────

    [HttpPut("users/{id:long}")]
    public async Task<IActionResult> UpdateUser(
        long id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) throw new NotFoundException();

        if (string.IsNullOrWhiteSpace(req.Email))       throw new ValidationException("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) throw new ValidationException("Display name is required.");

        var emailAddr = req.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == emailAddr && u.Id != id, ct))
            throw new ConflictException("Another user with that email already exists.");

        var oldEmail      = user.Email;
        var oldName       = user.DisplayName;
        var oldActive     = user.IsActive;
        var oldSuperAdmin = user.IsSuperAdmin;

        user.Email        = emailAddr;
        user.DisplayName  = req.DisplayName.Trim();
        user.IsActive     = req.IsActive;
        user.IsSuperAdmin = req.IsSuperAdmin;

        var changes = new List<string>();
        if (oldEmail      != user.Email)        changes.Add($"email → \"{user.Email}\"");
        if (oldName       != user.DisplayName)  changes.Add($"name → \"{user.DisplayName}\"");
        if (oldActive     != user.IsActive)     changes.Add(user.IsActive ? "activated" : "deactivated");
        if (oldSuperAdmin != user.IsSuperAdmin) changes.Add(user.IsSuperAdmin ? "promoted to super admin" : "demoted from super admin");

        var detail = changes.Count > 0 ? string.Join("; ", changes) + "." : "No changes.";
        Audit.Record("user", id, req.AuditClientId ?? 0, "updated",
            $"User \"{user.DisplayName}\" ({user.Email}): {detail}");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("users/{id:long}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(long id, [FromQuery] long? clientId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) throw new NotFoundException();

        user.IsActive = !user.IsActive;
        Audit.Record("user", id, clientId ?? 0, "toggled",
            user.IsActive
                ? $"User \"{user.DisplayName}\" ({user.Email}) activated."
                : $"User \"{user.DisplayName}\" ({user.Email}) deactivated.");
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPut("users/{id:long}/password")]
    public async Task<IActionResult> ChangePassword(
        long id, [FromBody] ChangePasswordRequest req, [FromQuery] long? clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");

        var user = await db.Users.FindAsync([id], ct);
        if (user is null) throw new NotFoundException();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        Audit.Record("user", id, clientId ?? 0, "password_changed",
            $"Password changed for \"{user.DisplayName}\" ({user.Email}) by admin.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("users/{id:long}")]
    public async Task<IActionResult> DeleteUser(long id, [FromQuery] long? clientId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) throw new NotFoundException();

        // Prevent deleting yourself
        if (user.Id == CurrentUser.Id)
            throw new ValidationException("You cannot delete your own account.");

        user.DeletedAt = DateTimeOffset.UtcNow;
        user.IsActive  = false;

        // Mangle email to free it for reuse (MySQL unique index doesn't support partial indexes)
        user.Email = $"{user.Email}_deleted_{user.Id}";

        // Also soft-delete all client access
        var accesses = await db.UserClientAccess
            .Where(a => a.UserId == id)
            .ToListAsync(ct);
        foreach (var access in accesses)
            access.DeletedAt = DateTimeOffset.UtcNow;

        // Invalidate all tokens
        var tokens = await db.UserTokens
            .Where(t => t.UserId == id)
            .ToListAsync(ct);
        foreach (var token in tokens)
            token.DeletedAt = DateTimeOffset.UtcNow;

        Audit.Record("user", id, clientId ?? 0, "deleted",
            $"User \"{user.DisplayName}\" ({user.Email}) deleted.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{id:long}/totp/disable")]
    public async Task<IActionResult> DisableTotp(long id, [FromQuery] long? clientId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) throw new NotFoundException();

        if (!user.IsTotpEnabled)
            throw new ValidationException("Two-factor authentication is not enabled for this user.");

        user.TotpSecret    = null;
        user.IsTotpEnabled = false;
        Audit.Record("user", id, clientId ?? 0, "totp_disabled",
            $"TOTP two-factor authentication disabled for \"{user.DisplayName}\" ({user.Email}) by admin.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Users — client access ────────────────────────────────────────────

    [HttpGet("users/{id:long}/clients")]
    public async Task<ActionResult<IReadOnlyList<UserClientAccessDto>>> GetUserClients(
        long id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) throw new NotFoundException();

        var accesses = await db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.UserId == id)
            .Join(db.Clients,
                  a => a.ClientId,
                  c => c.Id,
                  (a, c) => new { a, c })
            .OrderBy(x => x.c.Name)
            .Select(x => new UserClientAccessDto(
                x.c.Id, x.c.Name, x.c.Status, x.a.Role, x.a.GrantedAt))
            .ToListAsync(ct);

        return Ok(accesses);
    }

    [HttpPost("users/{id:long}/clients")]
    public async Task<IActionResult> GrantClientAccess(
        long id, [FromBody] GrantClientAccessRequest req, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id, ct))              throw new NotFoundException("User not found.");
        if (!await db.Clients.AnyAsync(c => c.Id == req.ClientId, ct))  throw new NotFoundException("Client not found.");

        var existing = await db.UserClientAccess
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.UserId == id && a.ClientId == req.ClientId, ct);

        if (existing is not null)
        {
            existing.Role      = req.Role;
            existing.DeletedAt = null;
            existing.GrantedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = id,
                ClientId  = req.ClientId,
                Role      = req.Role,
                GrantedAt = DateTimeOffset.UtcNow,
            });
        }

        Audit.Record("user_client_access", id, req.ClientId, "access_granted",
            $"Access granted as {req.Role}.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("users/{id:long}/clients/{clientId:long}")]
    public async Task<IActionResult> RevokeClientAccess(
        long id, long clientId, CancellationToken ct)
    {
        var access = await db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == id && a.ClientId == clientId, ct);

        if (access is null) throw new NotFoundException();

        access.DeletedAt = DateTimeOffset.UtcNow;
        Audit.Record("user_client_access", id, clientId, "access_revoked",
            "Access revoked.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Clients — user list & role ───────────────────────────────────────

    [HttpGet("clients/{id:long}/users")]
    public async Task<ActionResult<IReadOnlyList<AdminClientUserDto>>> GetClientUsers(
        long id, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == id, ct)) throw new NotFoundException();

        var users = await db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.ClientId == id)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new { a, u })
            .Where(x => !x.u.IsSuperAdmin)
            .OrderBy(x => x.u.DisplayName)
            .Select(x => new AdminClientUserDto(
                x.u.Id, x.u.DisplayName, x.u.Email,
                x.a.Role, x.u.IsActive, x.u.IsSuperAdmin))
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpPatch("clients/{id:long}/users/{userId:long}/role")]
    public async Task<IActionResult> UpdateClientUserRole(
        long id, long userId, [FromBody] UpdateClientUserRoleRequest req, CancellationToken ct)
    {
        var access = await db.UserClientAccess
            .FirstOrDefaultAsync(a => a.ClientId == id && a.UserId == userId, ct);
        if (access is null) throw new NotFoundException();

        var oldRole  = access.Role;
        access.Role  = req.Role;
        Audit.Record("user_client_access", userId, id, "role_changed",
            $"Role changed from {oldRole} to {req.Role}.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
