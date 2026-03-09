using BCrypt.Net;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Auth.Dtos;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Auth;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/auth/totp")]
public sealed class TotpController : ControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly ITotpService       _totp;
    private readonly IJwtService        _jwt;
    private readonly IConfiguration     _config;
    private readonly IAuditService      _audit;

    public TotpController(
        ImperaOpsDbContext db, ITotpService totp,
        IJwtService jwt, IConfiguration config, IAuditService audit)
    {
        _db     = db;
        _totp   = totp;
        _jwt    = jwt;
        _config = config;
        _audit  = audit;
    }

    // ── GET /api/v1/auth/totp/status ─────────────────────────────────────────

    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) throw new NotFoundException();

        return Ok(new { isTotpEnabled = user.IsTotpEnabled });
    }

    // ── POST /api/v1/auth/totp/setup ─────────────────────────────────────────

    [Authorize]
    [HttpPost("setup")]
    public async Task<IActionResult> Setup(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) throw new NotFoundException();

        var secret    = _totp.GenerateSecret();
        var issuer    = _config["App:Name"] ?? "ImperaOps";
        var qrCodeUri = _totp.GetQrCodeUri(secret, user.Email, issuer);

        user.TotpSecret    = secret;
        user.IsTotpEnabled = false; // not enabled until verified
        await _db.SaveChangesAsync(ct);

        return Ok(new { secret, qrCodeUri });
    }

    // ── POST /api/v1/auth/totp/verify-setup ──────────────────────────────────

    [Authorize]
    [HttpPost("verify-setup")]
    public async Task<IActionResult> VerifySetup(
        [FromBody] TotpCodeRequest req, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) throw new NotFoundException();

        if (string.IsNullOrWhiteSpace(user.TotpSecret))
            throw new ValidationException("TOTP setup has not been initiated. Call /setup first.");

        if (!_totp.Validate(user.TotpSecret, req.Code))
            throw new ValidationException("Invalid code. Please try again.");

        user.IsTotpEnabled = true;
        _audit.Record("user", userId, 0, "totp_enabled",
            $"TOTP two-factor authentication enabled by \"{user.DisplayName}\" ({user.Email}).",
            userId, user.DisplayName);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── POST /api/v1/auth/totp/disable ───────────────────────────────────────

    [Authorize]
    [HttpPost("disable")]
    public async Task<IActionResult> Disable(
        [FromBody] TotpDisableRequest req, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) throw new NotFoundException();

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new ValidationException("Password is incorrect.");

        user.TotpSecret    = null;
        user.IsTotpEnabled = false;
        _audit.Record("user", userId, 0, "totp_disabled",
            $"TOTP two-factor authentication disabled by \"{user.DisplayName}\" ({user.Email}).",
            userId, user.DisplayName);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── POST /api/v1/auth/totp/challenge ─────────────────────────────────────

    [HttpPost("challenge")]
    public async Task<ActionResult<AuthResultDto>> Challenge(
        [FromBody] TotpChallengeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PendingToken) || string.IsNullOrWhiteSpace(req.Code))
            throw new ValidationException("pendingToken and code are required.");

        var tokenRecord = await _db.UserTokens
            .FirstOrDefaultAsync(t => t.Token == req.PendingToken && t.Type == "TotpChallenge", ct);

        if (tokenRecord is null || tokenRecord.UsedAt.HasValue || tokenRecord.ExpiresAt < DateTimeOffset.UtcNow)
            return Unauthorized(new { message = "Challenge expired or invalid. Please sign in again." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == tokenRecord.UserId, ct);
        if (user is null || !user.IsActive || !user.IsTotpEnabled || user.TotpSecret is null)
            return Unauthorized(new { message = "Invalid challenge." });

        if (!_totp.Validate(user.TotpSecret, req.Code))
            return Unauthorized(new { message = "Invalid authenticator code." });

        // Mark challenge token as used
        tokenRecord.UsedAt = DateTimeOffset.UtcNow;

        // Get user's client access
        var clients = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .Join(_db.Clients.Where(c => c.Status != "Inactive"),
                  a => a.ClientId,
                  c => c.Id,
                  (a, c) => new Application.Auth.Dtos.ClientAccessDto(c.Id, c.Name, a.Role, c.ParentClientId))
            .ToListAsync(ct);

        // Revoke old sessions and create a new one
        await _db.UserTokens
            .Where(t => t.UserId == user.Id && t.Type == "Session")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DeletedAt, DateTimeOffset.UtcNow), ct);

        var ua           = Request.Headers["User-Agent"].ToString();
        var sessionToken = GenerateToken();
        var expiryMinutes = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "480");

        _db.UserTokens.Add(new UserToken
        {
            UserId      = user.Id,
            Token       = sessionToken,
            Type        = "Session",
            Description = ua.Length > 500 ? ua[..500] : ua,
            ExpiresAt   = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
            CreatedAt   = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        var jwt = _jwt.GenerateToken(user.Id, user.Email, user.DisplayName, user.IsSuperAdmin, clients, sessionToken);
        var activeClientName = user.ActiveClientId.HasValue
            ? (clients.FirstOrDefault(c => c.Id == user.ActiveClientId.Value)?.Name
               ?? await _db.Clients.Where(c => c.Id == user.ActiveClientId.Value).Select(c => c.Name).FirstOrDefaultAsync(ct))
            : null;
        return Ok(new AuthResultDto(jwt, user.DisplayName, user.Email, user.IsSuperAdmin, user.ActiveClientId, activeClientName, clients));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

public sealed record TotpCodeRequest(string Code);
public sealed record TotpDisableRequest(string Password);
public sealed record TotpChallengeRequest(string PendingToken, string Code);
