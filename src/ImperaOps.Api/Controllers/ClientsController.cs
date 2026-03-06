using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using ImperaOps.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients")]
public sealed class ClientsController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly IStorageService _storage;

    public ClientsController(ImperaOpsDbContext db, IEmailService email, IConfiguration config, IStorageService storage)
    {
        _db      = db;
        _email   = email;
        _config  = config;
        _storage = storage;
    }

    /// <summary>
    /// Returns branding for the given client. Available to any authenticated member of that client.
    /// </summary>
    [Authorize]
    [HttpGet("{clientId:long}/branding")]
    public async Task<ActionResult<ClientBrandingDto>> GetBranding(long clientId, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();

        var client = await _db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return NotFound();

        string? logoUrl = null;
        if (client.LogoStorageKey is not null)
        {
            try { logoUrl = await _storage.GetPresignedUrlAsync(client.LogoStorageKey, TimeSpan.FromHours(2)); }
            catch { /* return null if storage unavailable */ }
        }

        return Ok(new ClientBrandingDto(client.SystemName, client.PrimaryColor, client.LinkColor, logoUrl));
    }

    /// <summary>
    /// Returns active users who have access to the given client.
    /// </summary>
    [Authorize]
    [HttpGet("{clientId:long}/users")]
    public async Task<ActionResult<IReadOnlyList<ClientUserDto>>> GetClientUsers(
        long clientId, CancellationToken ct)
    {
        if (clientId == 0)
            return BadRequest("clientId must not be empty.");
        if (!HasClientAccess(clientId)) return NotFound();

        var rows = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .Join(_db.Users.Where(u => u.IsActive),
                a => a.UserId,
                u => u.Id,
                (a, u) => new { u.Id, u.DisplayName, u.Email, a.Role, u.IsActive, u.IsSuperAdmin })
            .OrderBy(x => x.DisplayName)
            .ToListAsync(ct);

        return Ok(rows.Select(x => new ClientUserDto(x.Id, x.DisplayName, x.Email, x.Role, x.IsActive, x.IsSuperAdmin)).ToList());
    }

    /// <summary>
    /// Adds an existing user to this client by email.
    /// </summary>
    [Authorize]
    [HttpPost("{clientId:long}/users")]
    public async Task<ActionResult<ClientUserDto>> AddUserToClient(
        long clientId, [FromBody] AddClientUserRequest req, CancellationToken ct)
    {
        if (clientId == 0) return BadRequest("clientId must not be empty.");
        if (!HasClientAccess(clientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Email)) return BadRequest("Email is required.");

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null) return NotFound("No user found with that email address.");
        if (!user.IsActive) return BadRequest("That user account is inactive.");

        var role = req.Role?.Trim() is { Length: > 0 } r ? r : "Member";

        var existingAccess = await _db.UserClientAccess
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.ClientId == clientId, ct);

        if (existingAccess is not null && existingAccess.DeletedAt == null)
            return Conflict("This user already has access to this client.");

        if (existingAccess is not null)
        {
            existingAccess.Role      = role;
            existingAccess.DeletedAt = null;
            existingAccess.GrantedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = user.Id,
                ClientId  = clientId,
                Role      = role,
                GrantedAt = DateTimeOffset.UtcNow,
            });
        }

        _db.AuditEvents.Add(MakeAudit("user_client_access", user.Id, clientId, "access_granted",
            $"User \"{user.Email}\" added as {role}."));
        await _db.SaveChangesAsync(ct);
        return Ok(new ClientUserDto(user.Id, user.DisplayName, user.Email, role, user.IsActive, user.IsSuperAdmin));
    }

    /// <summary>
    /// Updates the role of a user within this client.
    /// </summary>
    [Authorize]
    [HttpPatch("{clientId:long}/users/{userId:long}/role")]
    public async Task<IActionResult> UpdateUserRole(
        long clientId, long userId, [FromBody] UpdateClientUserRoleRequest req, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Role)) return BadRequest("Role is required.");

        var access = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ClientId == clientId, ct);

        if (access is null) return NotFound();

        access.Role = req.Role.Trim();
        _db.AuditEvents.Add(MakeAudit("user_client_access", userId, clientId, "role_changed",
            $"Role changed to {access.Role}."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Creates a new user account and immediately grants them access to this client.
    /// Sends an invitation email with a link to set their password.
    /// </summary>
    [Authorize]
    [HttpPost("{clientId:long}/invite")]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<ClientUserDto>> InviteUser(
        long clientId, [FromBody] InviteUserRequest req, CancellationToken ct)
    {
        if (clientId == 0)                               return BadRequest("clientId must not be empty.");
        if (!HasClientAccess(clientId))                  return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Email))        return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName))  return BadRequest("Display name is required.");

        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict("An account with that email already exists — use Add instead.");

        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return NotFound("Client not found.");

        var role = req.Role?.Trim() is { Length: > 0 } r ? r : "Member";

        var user = new AppUser
        {
            Email        = email,
            DisplayName  = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // locked until invite used
            IsActive     = true,
            IsSuperAdmin = false,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct); // flush to get user.Id

        _db.UserClientAccess.Add(new UserClientAccess
        {
            UserId    = user.Id,
            ClientId  = clientId,
            Role      = role,
            GrantedAt = DateTimeOffset.UtcNow,
        });

        var inviteToken = GenerateToken();
        _db.UserTokens.Add(new UserToken
        {
            UserId    = user.Id,
            Token     = inviteToken,
            Type      = "Invite",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        _db.AuditEvents.Add(MakeAudit("user", user.Id, clientId, "invited",
            $"User \"{user.Email}\" invited as {role}."));

        await _db.SaveChangesAsync(ct);

        var baseUrl   = _config["App:BaseUrl"] ?? "http://localhost:3000";
        var inviteUrl = $"{baseUrl}/set-password?token={inviteToken}";
        var emailSent = false;
        try
        {
            await _email.SendInviteAsync(user.Email, user.DisplayName, inviteUrl, ct);
            emailSent = true;
        }
        catch { /* non-fatal — invite URL is returned so admin can share it manually */ }

        var dto = new ClientUserDto(user.Id, user.DisplayName, user.Email, role, true, false);
        return Ok(new ImperaOps.Api.Contracts.InviteUserResult<ClientUserDto>(dto, inviteUrl, emailSent));
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Returns users from related clients (same parent/child family) who don't yet have
    /// access to this client. Super admin only.
    /// </summary>
    [Authorize]
    [HttpGet("{clientId:long}/family-users")]
    public async Task<ActionResult<IReadOnlyList<ClientUserDto>>> GetFamilyUsers(
        long clientId, CancellationToken ct)
    {
        if (!IsSuperAdmin) return Forbid();

        var client = await _db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return NotFound();

        var rootId = client.ParentClientId ?? clientId;

        var familyIds = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == rootId || c.ParentClientId == rootId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var existingIds = await _db.UserClientAccess.AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .Select(a => a.UserId)
            .ToListAsync(ct);

        var eligibleIds = await _db.UserClientAccess.AsNoTracking()
            .Where(a => familyIds.Contains(a.ClientId) && !existingIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(ct);

        var users = await _db.Users.AsNoTracking()
            .Where(u => eligibleIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new ClientUserDto(u.Id, u.DisplayName, u.Email, "Member", u.IsActive, u.IsSuperAdmin))
            .ToListAsync(ct);

        return Ok(users);
    }

    /// <summary>
    /// Updates a user's display name and email. User must have access to this client.
    /// </summary>
    [Authorize]
    [HttpPut("{clientId:long}/users/{userId:long}")]
    public async Task<IActionResult> UpdateClientUser(
        long clientId, long userId, [FromBody] UpdateClientUserRequest req, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return BadRequest("Display name is required.");
        if (string.IsNullOrWhiteSpace(req.Email))       return BadRequest("Email is required.");

        var hasAccess = await _db.UserClientAccess
            .AnyAsync(a => a.UserId == userId && a.ClientId == clientId, ct);
        if (!hasAccess) return NotFound();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound();

        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != userId, ct))
            return Conflict("That email address is already in use.");

        var oldName  = user.DisplayName;
        var oldEmail = user.Email;

        user.DisplayName = req.DisplayName.Trim();
        user.Email       = email;

        var changes = new List<string>();
        if (oldName  != user.DisplayName) changes.Add($"name → \"{user.DisplayName}\"");
        if (oldEmail != user.Email)       changes.Add($"email → \"{user.Email}\"");

        _db.AuditEvents.Add(MakeAudit("user", userId, clientId, "updated",
            changes.Count > 0 ? string.Join("; ", changes) + "." : "Updated."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Removes a user's access to this client.
    /// </summary>
    [Authorize]
    [HttpDelete("{clientId:long}/users/{userId:long}")]
    public async Task<IActionResult> RemoveUserFromClient(
        long clientId, long userId, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();

        var access = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ClientId == clientId, ct);

        if (access is null) return NotFound();

        access.DeletedAt = DateTimeOffset.UtcNow;
        _db.AuditEvents.Add(MakeAudit("user_client_access", userId, clientId, "access_revoked",
            "User removed from client."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record ClientUserDto(long Id, string DisplayName, string Email, string Role, bool IsActive, bool IsSuperAdmin);
public sealed record AddClientUserRequest(string Email, string? Role);
public sealed record InviteUserRequest(string Email, string DisplayName, string? Role);
public sealed record UpdateClientUserRoleRequest(string Role);
public sealed record UpdateClientUserRequest(string DisplayName, string Email);
