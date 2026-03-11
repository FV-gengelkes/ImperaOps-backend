using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:long}/api-credentials")]
[Authorize]
public sealed class ApiCredentialsController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public ApiCredentialsController(ImperaOpsDbContext db) => _db = db;

    private static readonly string[] ValidScopes = ["events:create", "events:update", "events:read"];

    // GET api/v1/clients/{clientId}/api-credentials
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiCredentialDto>>> List(
        long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) throw new NotFoundException();

        var items = await _db.ApiCredentials.AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return Ok(items.Select(c => ToDto(c, client.ClientSid)).ToList());
    }

    // POST api/v1/clients/{clientId}/api-credentials
    [HttpPost]
    public async Task<ActionResult<ApiCredentialCreatedDto>> Create(
        long clientId, [FromBody] CreateApiCredentialRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Name is required.");
        if (req.Scopes is null || req.Scopes.Length == 0)
            throw new ValidationException("At least one scope is required.");

        var invalidScopes = req.Scopes.Except(ValidScopes).ToArray();
        if (invalidScopes.Length > 0)
            throw new ValidationException($"Invalid scopes: {string.Join(", ", invalidScopes)}. Valid: {string.Join(", ", ValidScopes)}");

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) throw new NotFoundException();

        // Generate secure key ID and secret
        var keyId = $"key_{GenerateRandomString(12)}";
        var secret = $"sk_live_{GenerateRandomString(40)}";
        var secretHash = HashSecret(secret);
        var secretLast4 = secret[^4..];

        var now = DateTimeOffset.UtcNow;
        var credential = new ApiCredential
        {
            ClientId = clientId,
            Name = req.Name.Trim(),
            KeyId = keyId,
            SecretHash = secretHash,
            SecretLast4 = secretLast4,
            ScopesJson = JsonSerializer.Serialize(req.Scopes),
            Status = "active",
            CreatedByUserId = CurrentUserId(),
            ExpiresAt = req.ExpiresAt,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.ApiCredentials.Add(credential);

        // Audit
        _db.ApiCredentialAuditLogs.Add(new ApiCredentialAuditLog
        {
            ClientId = clientId,
            ApiCredentialId = 0, // will be set after save
            Action = "created",
            PerformedByUserId = CurrentUserId(),
            DetailsJson = JsonSerializer.Serialize(new { credential.Name, credential.KeyId, Scopes = req.Scopes }),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(ct);

        // Fix audit log with actual credential ID
        var auditLog = await _db.ApiCredentialAuditLogs
            .Where(a => a.ApiCredentialId == 0 && a.ClientId == clientId)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);
        if (auditLog is not null)
        {
            auditLog.ApiCredentialId = credential.Id;
            await _db.SaveChangesAsync(ct);
        }

        var authHeader = $"Bearer {client.ClientSid}.{keyId}.{secret}";

        return Ok(new ApiCredentialCreatedDto(
            credential.Id,
            client.ClientSid,
            keyId,
            secret,
            credential.Name,
            req.Scopes,
            authHeader
        ));
    }

    // PUT api/v1/clients/{clientId}/api-credentials/{id}
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiCredentialDto>> Update(
        long clientId, long id, [FromBody] UpdateApiCredentialRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var credential = await _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId, ct);
        if (credential is null) throw new NotFoundException();

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Name is required.");
        if (req.Scopes is null || req.Scopes.Length == 0)
            throw new ValidationException("At least one scope is required.");

        var invalidScopes = req.Scopes.Except(ValidScopes).ToArray();
        if (invalidScopes.Length > 0)
            throw new ValidationException($"Invalid scopes: {string.Join(", ", invalidScopes)}.");

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) throw new NotFoundException();

        credential.Name = req.Name.Trim();
        credential.ScopesJson = JsonSerializer.Serialize(req.Scopes);
        credential.ExpiresAt = req.ExpiresAt;
        credential.UpdatedAt = DateTimeOffset.UtcNow;

        if (!req.IsActive && credential.Status == "active")
        {
            credential.Status = "revoked";
            credential.RevokedAt = DateTimeOffset.UtcNow;
            credential.RevokedByUserId = CurrentUserId();

            _db.ApiCredentialAuditLogs.Add(new ApiCredentialAuditLog
            {
                ClientId = clientId,
                ApiCredentialId = id,
                Action = "revoked",
                PerformedByUserId = CurrentUserId(),
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        else if (req.IsActive && credential.Status == "revoked")
        {
            credential.Status = "active";
            credential.RevokedAt = null;
            credential.RevokedByUserId = null;

            _db.ApiCredentialAuditLogs.Add(new ApiCredentialAuditLog
            {
                ClientId = clientId,
                ApiCredentialId = id,
                Action = "updated",
                PerformedByUserId = CurrentUserId(),
                DetailsJson = JsonSerializer.Serialize(new { Action = "reactivated" }),
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(credential, client.ClientSid));
    }

    // POST api/v1/clients/{clientId}/api-credentials/{id}/revoke
    [HttpPost("{id:long}/revoke")]
    public async Task<IActionResult> Revoke(long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var credential = await _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId, ct);
        if (credential is null) throw new NotFoundException();

        if (credential.Status == "revoked")
            throw new ConflictException("Credential is already revoked.");

        credential.Status = "revoked";
        credential.RevokedAt = DateTimeOffset.UtcNow;
        credential.RevokedByUserId = CurrentUserId();
        credential.UpdatedAt = DateTimeOffset.UtcNow;

        _db.ApiCredentialAuditLogs.Add(new ApiCredentialAuditLog
        {
            ClientId = clientId,
            ApiCredentialId = id,
            Action = "revoked",
            PerformedByUserId = CurrentUserId(),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // GET api/v1/clients/{clientId}/api-credentials/{id}/audit
    [HttpGet("{id:long}/audit")]
    public async Task<ActionResult<IReadOnlyList<ApiCredentialAuditLogDto>>> GetAuditLogs(
        long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var logs = await _db.ApiCredentialAuditLogs.AsNoTracking()
            .Where(a => a.ApiCredentialId == id && a.ClientId == clientId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(logs.Select(a => new ApiCredentialAuditLogDto(
            a.Id, a.Action, a.PerformedByUserId, a.DetailsJson, a.CreatedAt.ToString("o")
        )).ToList());
    }

    // DELETE api/v1/clients/{clientId}/api-credentials/{id}
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var credential = await _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId, ct);
        if (credential is null) throw new NotFoundException();

        credential.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ApiCredentialDto ToDto(ApiCredential c, string clientSid)
    {
        var scopes = JsonSerializer.Deserialize<string[]>(c.ScopesJson) ?? [];
        return new ApiCredentialDto(
            c.Id, c.ClientId, clientSid, c.Name, c.KeyId, c.SecretLast4,
            scopes, c.Status, c.LastUsedAt?.ToString("o"), c.LastUsedIp,
            c.ExpiresAt?.ToString("o"), c.CreatedAt.ToString("o")
        );
    }

    internal static string HashSecret(string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(bytes);
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        Span<byte> random = stackalloc byte[length];
        RandomNumberGenerator.Fill(random);
        for (int i = 0; i < length; i++)
            result[i] = chars[random[i] % chars.Length];
        return new string(result);
    }
}
