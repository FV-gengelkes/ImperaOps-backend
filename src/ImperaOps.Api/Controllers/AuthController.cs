using BCrypt.Net;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Auth.Commands;
using ImperaOps.Application.Auth.Dtos;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ImperaOpsDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;

    public AuthController(IMediator mediator, ImperaOpsDbContext db, IJwtService jwt, IEmailService email, IConfiguration config)
    {
        _mediator = mediator;
        _db       = db;
        _jwt      = jwt;
        _email    = email;
        _config   = config;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var creds = await _mediator.Send(request, ct);

            // Check if TOTP is enabled for this user
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == creds.UserId, ct);

            if (user?.IsTotpEnabled == true)
            {
                // Invalidate any prior unused TOTP challenge tokens
                await _db.UserTokens
                    .Where(t => t.UserId == user.Id && t.Type == "TotpChallenge" && t.UsedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedAt, DateTimeOffset.UtcNow), ct);

                var challengeToken = GenerateToken();
                _db.UserTokens.Add(new UserToken
                {
                    UserId    = user.Id,
                    Token     = challengeToken,
                    Type      = "TotpChallenge",
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await _db.SaveChangesAsync(ct);

                return Ok(new { totpRequired = true, pendingToken = challengeToken });
            }

            // Enforce single session: revoke all existing sessions before creating a new one
            var sessionToken = await CreateSessionAsync(creds.UserId, revokeAll: true, ct);
            var jwt = _jwt.GenerateToken(creds.UserId, creds.Email, creds.DisplayName, creds.IsSuperAdmin, creds.Clients, sessionToken);

            return Ok(new AuthResultDto(jwt, creds.DisplayName, creds.Email, creds.IsSuperAdmin, creds.Clients));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var sub         = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var email       = User.FindFirstValue(ClaimTypes.Email)          ?? User.FindFirstValue("email");
        var displayName = User.FindFirstValue("display_name");
        var clientIds   = User.FindAll("client_id").Select(c => c.Value).ToList();

        return Ok(new { userId = sub, email, displayName, clientIds });
    }

    /// <summary>Updates the authenticated user's display name and email.</summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest("Display name is required.");

        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest("A valid email address is required.");

        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal))
        {
            var taken = await _db.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != userId, ct);
            if (taken) return BadRequest("That email address is already in use.");
        }

        user.DisplayName = req.DisplayName.Trim();
        user.Email       = normalizedEmail;
        await _db.SaveChangesAsync(ct);

        return Ok(new { displayName = user.DisplayName, email = user.Email });
    }

    /// <summary>Changes the authenticated user's password after verifying the current one.</summary>
    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword))
            return BadRequest("Current password is required.");

        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest("New password must be at least 8 characters.");

        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return BadRequest("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync(ct);

        return Ok();
    }

    // ── Forgot password ──────────────────────────────────────────────────────

    [HttpPost("forgot-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        // Always return 200 — never reveal whether the email exists.
        if (string.IsNullOrWhiteSpace(req.Email)) return Ok();

        var email = req.Email.Trim().ToLowerInvariant();
        var user  = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        if (user is null) return Ok();

        // Invalidate any prior unused reset tokens for this user
        var oldTokens = await _db.UserTokens
            .Where(t => t.UserId == user.Id && t.Type == "PasswordReset" && t.UsedAt == null)
            .ToListAsync(ct);
        _db.UserTokens.RemoveRange(oldTokens);

        var token = GenerateToken();
        _db.UserTokens.Add(new UserToken
        {
            UserId    = user.Id,
            Token     = token,
            Type      = "PasswordReset",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        var baseUrl  = _config["App:BaseUrl"] ?? "http://localhost:3000";
        var resetUrl = $"{baseUrl}/set-password?token={token}";
        await _email.SendPasswordResetAsync(user.Email, user.DisplayName, resetUrl, ct);

        return Ok();
    }

    [HttpGet("validate-token")]
    public async Task<IActionResult> ValidateToken([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Ok(new { valid = false });

        var record = await _db.UserTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (record is null || record.UsedAt.HasValue || record.ExpiresAt < DateTimeOffset.UtcNow)
            return Ok(new { valid = false });

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == record.UserId, ct);
        if (user is null) return Ok(new { valid = false });

        return Ok(new { valid = true, type = record.Type, email = user.Email });
    }

    [HttpPost("set-password")]
    public async Task<ActionResult<AuthResultDto>> SetPassword(
        [FromBody] SetPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest("Token is required.");
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        var record = await _db.UserTokens
            .FirstOrDefaultAsync(t => t.Token == req.Token, ct);

        if (record is null || record.UsedAt.HasValue || record.ExpiresAt < DateTimeOffset.UtcNow)
            return BadRequest("This link is invalid or has expired.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == record.UserId, ct);
        if (user is null) return BadRequest("User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.IsActive     = true; // Ensure invited users are active
        record.UsedAt     = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var clients = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .Join(_db.Clients.Where(c => c.IsActive),
                  a => a.ClientId,
                  c => c.Id,
                  (a, c) => new ClientAccessDto(c.Id, c.Name, a.Role, c.ParentClientId))
            .ToListAsync(ct);

        var sessionToken = await CreateSessionAsync(user.Id, revokeAll: true, ct);
        var jwtToken = _jwt.GenerateToken(user.Id, user.Email, user.DisplayName, user.IsSuperAdmin, clients, sessionToken);

        return Ok(new AuthResultDto(jwtToken, user.DisplayName, user.Email, user.IsSuperAdmin, clients));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Optionally revokes all existing sessions then creates a fresh Session token. Returns the new token value.</summary>
    private async Task<string> CreateSessionAsync(long userId, bool revokeAll, CancellationToken ct)
    {
        if (revokeAll)
        {
            await _db.UserTokens
                .Where(t => t.UserId == userId && t.Type == "Session")
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedAt, DateTimeOffset.UtcNow), ct);
        }

        var ua = Request.Headers["User-Agent"].ToString();
        var sessionToken = GenerateToken();
        var expiryMinutes = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "480");

        _db.UserTokens.Add(new UserToken
        {
            UserId      = userId,
            Token       = sessionToken,
            Type        = "Session",
            Description = ua.Length > 500 ? ua[..500] : ua,
            ExpiresAt   = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
            CreatedAt   = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return sessionToken;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

public sealed record UpdateProfileRequest(string DisplayName, string Email);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ForgotPasswordRequest(string Email);
public sealed record SetPasswordRequest(string Token, string NewPassword);
