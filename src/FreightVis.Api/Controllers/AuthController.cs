using BCrypt.Net;
using FreightVis.Application.Auth.Commands;
using FreightVis.Application.Auth.Dtos;
using FreightVis.Infrastructure.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly FreightVisDbContext _db;

    public AuthController(IMediator mediator, FreightVisDbContext db)
    {
        _mediator = mediator;
        _db       = db;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(request, ct);
            return Ok(result);
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

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
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

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return BadRequest("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync(ct);

        return Ok();
    }
}

public sealed record UpdateProfileRequest(string DisplayName, string Email);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
