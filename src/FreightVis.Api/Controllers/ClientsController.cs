using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly FreightVisDbContext _db;

    public ClientsController(FreightVisDbContext db) => _db = db;

    /// <summary>
    /// Returns active users who have access to the given client.
    /// </summary>
    [HttpGet("{clientId:guid}/users")]
    public async Task<ActionResult<IReadOnlyList<ClientUserDto>>> GetClientUsers(
        Guid clientId, CancellationToken ct)
    {
        if (clientId == Guid.Empty)
            return BadRequest("clientId must not be empty.");

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
    [HttpPost("{clientId:guid}/users")]
    public async Task<ActionResult<ClientUserDto>> AddUserToClient(
        Guid clientId, [FromBody] AddClientUserRequest req, CancellationToken ct)
    {
        if (clientId == Guid.Empty) return BadRequest("clientId must not be empty.");
        if (string.IsNullOrWhiteSpace(req.Email)) return BadRequest("Email is required.");

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null) return NotFound("No user found with that email address.");
        if (!user.IsActive) return BadRequest("That user account is inactive.");

        var existing = await _db.UserClientAccess
            .AnyAsync(a => a.UserId == user.Id && a.ClientId == clientId, ct);

        if (existing) return Conflict("This user already has access to this client.");

        var role = req.Role?.Trim() is { Length: > 0 } r ? r : "Member";

        _db.UserClientAccess.Add(new UserClientAccess
        {
            UserId    = user.Id,
            ClientId  = clientId,
            Role      = role,
            GrantedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new ClientUserDto(user.Id, user.DisplayName, user.Email, role, user.IsActive, user.IsSuperAdmin));
    }

    /// <summary>
    /// Updates the role of a user within this client.
    /// </summary>
    [HttpPatch("{clientId:guid}/users/{userId:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(
        Guid clientId, Guid userId, [FromBody] UpdateClientUserRoleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Role)) return BadRequest("Role is required.");

        var access = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ClientId == clientId, ct);

        if (access is null) return NotFound();

        access.Role = req.Role.Trim();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Creates a new user account and immediately grants them access to this client.
    /// Used when inviting someone who does not yet have an account.
    /// </summary>
    [HttpPost("{clientId:guid}/invite")]
    public async Task<ActionResult<ClientUserDto>> InviteUser(
        Guid clientId, [FromBody] InviteUserRequest req, CancellationToken ct)
    {
        if (clientId == Guid.Empty)          return BadRequest("clientId must not be empty.");
        if (string.IsNullOrWhiteSpace(req.Email))       return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return BadRequest("Display name is required.");
        if (string.IsNullOrWhiteSpace(req.Password))    return BadRequest("Password is required.");
        if (req.Password.Length < 8)                    return BadRequest("Password must be at least 8 characters.");

        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict("An account with that email already exists — use Add instead.");

        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return NotFound("Client not found.");

        var role = req.Role?.Trim() is { Length: > 0 } r ? r : "Member";

        var user = new AppUser
        {
            Id           = Guid.NewGuid(),
            Email        = email,
            DisplayName  = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsActive     = true,
            IsSuperAdmin = false,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

        _db.Users.Add(user);
        _db.UserClientAccess.Add(new UserClientAccess
        {
            UserId    = user.Id,
            ClientId  = clientId,
            Role      = role,
            GrantedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new ClientUserDto(user.Id, user.DisplayName, user.Email, role, true, false));
    }

    /// <summary>
    /// Returns users from related clients (same parent/child family) who don't yet have
    /// access to this client. Super admin only.
    /// </summary>
    [Authorize]
    [HttpGet("{clientId:guid}/family-users")]
    public async Task<ActionResult<IReadOnlyList<ClientUserDto>>> GetFamilyUsers(
        Guid clientId, CancellationToken ct)
    {
        if (User.FindFirstValue("is_super_admin") != "true") return Forbid();

        var client = await _db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return NotFound();

        // Walk up to the root of the hierarchy
        var rootId = client.ParentClientId ?? clientId;

        // All clients in the family: root + every client whose parent is root
        var familyIds = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == rootId || c.ParentClientId == rootId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        // Users already granted access to this client
        var existingIds = await _db.UserClientAccess.AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .Select(a => a.UserId)
            .ToListAsync(ct);

        // Distinct user IDs from family clients not already in this client
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
    [HttpPut("{clientId:guid}/users/{userId:guid}")]
    public async Task<IActionResult> UpdateClientUser(
        Guid clientId, Guid userId, [FromBody] UpdateClientUserRequest req, CancellationToken ct)
    {
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

        user.DisplayName = req.DisplayName.Trim();
        user.Email       = email;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Removes a user's access to this client.
    /// </summary>
    [HttpDelete("{clientId:guid}/users/{userId:guid}")]
    public async Task<IActionResult> RemoveUserFromClient(
        Guid clientId, Guid userId, CancellationToken ct)
    {
        var access = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ClientId == clientId, ct);

        if (access is null) return NotFound();

        _db.UserClientAccess.Remove(access);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record ClientUserDto(Guid Id, string DisplayName, string Email, string Role, bool IsActive, bool IsSuperAdmin);
public sealed record AddClientUserRequest(string Email, string? Role);
public sealed record InviteUserRequest(string Email, string DisplayName, string Password, string? Role);
public sealed record UpdateClientUserRoleRequest(string Role);
public sealed record UpdateClientUserRequest(string DisplayName, string Email);
