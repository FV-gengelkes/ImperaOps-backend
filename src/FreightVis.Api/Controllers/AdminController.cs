using FreightVis.Api.Contracts;
using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "SuperAdmin")]
public sealed class AdminController : ControllerBase
{
    private readonly FreightVisDbContext _db;

    public AdminController(FreightVisDbContext db) => _db = db;

    // ── Clients ──────────────────────────────────────────────────────────

    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<AdminClientDto>>> GetClients(CancellationToken ct)
    {
        var clients = await _db.Clients.AsNoTracking()
            .Where(c => c.Id != Guid.Empty)
            .ToListAsync(ct);

        var userCounts = await _db.UserClientAccess
            .AsNoTracking()
            .GroupBy(a => a.ClientId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var parentNames = clients.ToDictionary(c => c.Id, c => c.Name);

        var result = clients
            .OrderBy(c => c.Name)
            .Select(c => new AdminClientDto(
                c.Id, c.Name, c.ParentClientId,
                c.ParentClientId.HasValue && parentNames.TryGetValue(c.ParentClientId.Value, out var pn) ? pn : null,
                c.IsActive,
                userCounts.TryGetValue(c.Id, out var cnt) ? cnt : 0,
                c.CreatedAt))
            .ToList();

        return Ok(result);
    }

    [HttpPost("clients")]
    public async Task<ActionResult<AdminClientDto>> CreateClient(
        [FromBody] CreateClientRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        if (req.ParentClientId.HasValue)
        {
            var parent = await _db.Clients.FindAsync([req.ParentClientId.Value], ct);
            if (parent is null) return BadRequest("Parent client not found.");
        }

        var client = new Client
        {
            Id             = Guid.NewGuid(),
            Name           = req.Name.Trim(),
            ParentClientId = req.ParentClientId,
            IsActive       = true,
            CreatedAt      = DateTimeOffset.UtcNow,
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetClients), new AdminClientDto(
            client.Id, client.Name, client.ParentClientId, null,
            client.IsActive, 0, client.CreatedAt));
    }

    [HttpPut("clients/{id:guid}")]
    public async Task<IActionResult> UpdateClient(
        Guid id, [FromBody] UpdateClientRequest req, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        if (req.ParentClientId == id) return BadRequest("A client cannot be its own parent.");

        client.Name           = req.Name.Trim();
        client.ParentClientId = req.ParentClientId;
        client.IsActive       = req.IsActive;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("clients/{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleClientActive(Guid id, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        client.IsActive = !client.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new { client.Id, client.IsActive });
    }

    // ── Users — list & create ────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().ToListAsync(ct);

        var clientCounts = await _db.UserClientAccess
            .AsNoTracking()
            .GroupBy(a => a.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return Ok(users
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserDto(
                u.Id, u.Email, u.DisplayName, u.IsActive, u.IsSuperAdmin,
                clientCounts.TryGetValue(u.Id, out var cnt) ? cnt : 0,
                u.CreatedAt))
            .ToList());
    }

    [HttpPost("users")]
    public async Task<ActionResult<AdminUserDto>> CreateUser(
        [FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))       return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return BadRequest("Display name is required.");
        if (string.IsNullOrWhiteSpace(req.Password))    return BadRequest("Password is required.");
        if (req.Password.Length < 8)                    return BadRequest("Password must be at least 8 characters.");

        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict("A user with that email already exists.");

        var user = new AppUser
        {
            Id           = Guid.NewGuid(),
            Email        = email,
            DisplayName  = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsActive     = true,
            IsSuperAdmin = req.IsSuperAdmin,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetUsers),
            new AdminUserDto(user.Id, user.Email, user.DisplayName,
                user.IsActive, user.IsSuperAdmin, 0, user.CreatedAt));
    }

    // ── Users — update & toggle ──────────────────────────────────────────

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(
        Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Email))       return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return BadRequest("Display name is required.");

        var email = req.Email.Trim().ToLowerInvariant();

        // Check email uniqueness (exclude self)
        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct))
            return Conflict("Another user with that email already exists.");

        user.Email       = email;
        user.DisplayName = req.DisplayName.Trim();
        user.IsActive    = req.IsActive;
        user.IsSuperAdmin = req.IsSuperAdmin;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("users/{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPut("users/{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(
        Guid id, [FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Users — client access ────────────────────────────────────────────

    [HttpGet("users/{id:guid}/clients")]
    public async Task<ActionResult<IReadOnlyList<UserClientAccessDto>>> GetUserClients(
        Guid id, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == id, ct)) return NotFound();

        var accesses = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.UserId == id)
            .Join(_db.Clients,
                  a => a.ClientId,
                  c => c.Id,
                  (a, c) => new { a, c })
            .OrderBy(x => x.c.Name)
            .Select(x => new UserClientAccessDto(
                x.c.Id, x.c.Name, x.c.IsActive, x.a.Role, x.a.GrantedAt))
            .ToListAsync(ct);

        return Ok(accesses);
    }

    [HttpPost("users/{id:guid}/clients")]
    public async Task<IActionResult> GrantClientAccess(
        Guid id, [FromBody] GrantClientAccessRequest req, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == id, ct))   return NotFound("User not found.");
        if (!await _db.Clients.AnyAsync(c => c.Id == req.ClientId, ct)) return NotFound("Client not found.");

        var existing = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == id && a.ClientId == req.ClientId, ct);

        if (existing is not null)
        {
            // Update role if access already exists
            existing.Role = req.Role;
        }
        else
        {
            _db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = id,
                ClientId  = req.ClientId,
                Role      = req.Role,
                GrantedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("users/{id:guid}/clients/{clientId:guid}")]
    public async Task<IActionResult> RevokeClientAccess(
        Guid id, Guid clientId, CancellationToken ct)
    {
        var access = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == id && a.ClientId == clientId, ct);

        if (access is null) return NotFound();

        _db.UserClientAccess.Remove(access);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
