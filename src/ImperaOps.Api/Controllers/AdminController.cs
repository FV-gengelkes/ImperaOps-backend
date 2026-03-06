using ImperaOps.Api.Contracts;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using ImperaOps.Infrastructure.Storage;
using ImperaOps.Infrastructure.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "SuperAdmin")]
public sealed class AdminController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly IStorageService _storage;

    public AdminController(ImperaOpsDbContext db, IEmailService email, IConfiguration config, IStorageService storage)
    {
        _db      = db;
        _email   = email;
        _config  = config;
        _storage = storage;
    }

    // ── Clients ──────────────────────────────────────────────────────────

    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<AdminClientDto>>> GetClients(CancellationToken ct)
    {
        var clients = await _db.Clients.AsNoTracking()
            .ToListAsync(ct);

        var userCounts = await _db.UserClientAccess
            .AsNoTracking()
            .Join(_db.Users, a => a.UserId, u => u.Id, (a, u) => new { a, u })
            .Where(x => !x.u.IsSuperAdmin)
            .GroupBy(x => x.a.ClientId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var parentNames = clients.ToDictionary(c => c.Id, c => c.Name);

        var result = clients
            .OrderBy(c => c.Name)
            .Select(c => new AdminClientDto(
                c.Id, c.Name, c.Slug, c.ParentClientId,
                c.ParentClientId.HasValue && parentNames.TryGetValue(c.ParentClientId.Value, out var pn) ? pn : null,
                c.IsActive,
                userCounts.TryGetValue(c.Id, out var cnt) ? cnt : 0,
                c.CreatedAt,
                ParseTemplateIds(c.AppliedTemplateIds)))
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

        EventTemplateDefinition? template = null;
        if (!string.IsNullOrWhiteSpace(req.TemplateId))
        {
            if (!TemplateLibrary.All.TryGetValue(req.TemplateId, out template))
                return BadRequest("Template not found.");
        }

        var client = new Client
        {
            Name           = req.Name.Trim(),
            Slug           = GenerateSlug(req.Name),
            ParentClientId = req.ParentClientId,
            IsActive       = true,
            CreatedAt      = DateTimeOffset.UtcNow,
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct); // flush to get client.Id

        var auditBody = template is not null
            ? $"Client \"{client.Name}\" created with template \"{template.Name}\"."
            : $"Client \"{client.Name}\" created.";
        _db.AuditEvents.Add(MakeAudit("client", client.Id, client.Id, "created", auditBody));
        await _db.SaveChangesAsync(ct);

        if (template is not null)
        {
            await ApplyTemplateToClientAsync(client.Id, template, ct);
            client.AppliedTemplateIds = JsonSerializer.Serialize(new[] { template.Id });
            await _db.SaveChangesAsync(ct);
        }

        return CreatedAtAction(nameof(GetClients), new AdminClientDto(
            client.Id, client.Name, client.Slug, client.ParentClientId, null,
            client.IsActive, 0, client.CreatedAt,
            ParseTemplateIds(client.AppliedTemplateIds)));
    }

    [HttpPut("clients/{id:long}")]
    public async Task<IActionResult> UpdateClient(
        long id, [FromBody] UpdateClientRequest req, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        if (req.ParentClientId == id) return BadRequest("A client cannot be its own parent.");

        var oldName   = client.Name;
        var oldActive = client.IsActive;
        var oldParent = client.ParentClientId;

        client.Name           = req.Name.Trim();
        client.ParentClientId = req.ParentClientId;
        client.IsActive       = req.IsActive;

        var changes = new List<string>();
        if (oldName   != client.Name)           changes.Add($"name → \"{client.Name}\"");
        if (oldActive != client.IsActive)       changes.Add(client.IsActive ? "activated" : "deactivated");
        if (oldParent != client.ParentClientId) changes.Add("parent changed");

        _db.AuditEvents.Add(MakeAudit("client", id, id, "updated",
            changes.Count > 0 ? string.Join("; ", changes) + "." : "Updated."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("clients/{id:long}/toggle-active")]
    public async Task<IActionResult> ToggleClientActive(long id, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        client.IsActive = !client.IsActive;
        _db.AuditEvents.Add(MakeAudit("client", id, id, "toggled",
            client.IsActive ? "Client activated." : "Client deactivated."));
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
                u.CreatedAt,
                u.IsTotpEnabled))
            .ToList());
    }

    [HttpPost("users")]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<AdminUserDto>> CreateUser(
        [FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))       return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return BadRequest("Display name is required.");

        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict("A user with that email already exists.");

        var user = new AppUser
        {
            Email        = email,
            DisplayName  = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // locked until invite used
            IsActive     = true,
            IsSuperAdmin = req.IsSuperAdmin,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct); // flush to get user.Id

        // Optionally associate to a client on creation
        if (req.ClientId.HasValue)
        {
            var client = await _db.Clients.FindAsync([req.ClientId.Value], ct);
            if (client is null) return BadRequest("Client not found.");

            _db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = user.Id,
                ClientId  = req.ClientId.Value,
                Role      = req.Role ?? "Member",
                GrantedAt = DateTimeOffset.UtcNow,
            });
        }

        var auditClientId = req.ClientId ?? req.AuditClientId ?? 0;
        var inviteToken = GenerateToken();
        _db.UserTokens.Add(new UserToken
        {
            UserId    = user.Id,
            Token     = inviteToken,
            Type      = "Invite",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        _db.AuditEvents.Add(MakeAudit("user", user.Id, auditClientId, "created",
            $"User \"{user.Email}\" created{(user.IsSuperAdmin ? " as super admin" : "")}."));

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

        var dto = new AdminUserDto(user.Id, user.Email, user.DisplayName,
            user.IsActive, user.IsSuperAdmin, req.ClientId.HasValue ? 1 : 0, user.CreatedAt, user.IsTotpEnabled);
        return CreatedAtAction(nameof(GetUsers), new InviteUserResult<AdminUserDto>(dto, inviteUrl, emailSent));
    }

    // ── Client Branding ──────────────────────────────────────────────────

    [HttpGet("clients/{id:long}/branding")]
    public async Task<ActionResult<ClientBrandingDto>> GetBranding(long id, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();
        return Ok(await BuildBrandingDtoAsync(client, ct));
    }

    [HttpPut("clients/{id:long}/branding")]
    public async Task<IActionResult> UpdateBranding(
        long id, [FromBody] UpdateBrandingRequest req, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        if (req.PrimaryColor is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(req.PrimaryColor, @"^#[0-9A-Fa-f]{6}$"))
            return BadRequest("PrimaryColor must be a valid 6-digit hex color, e.g. #2F80ED.");

        if (req.LinkColor is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(req.LinkColor, @"^#[0-9A-Fa-f]{6}$"))
            return BadRequest("LinkColor must be a valid 6-digit hex color, e.g. #1A5FB4.");

        client.SystemName   = string.IsNullOrWhiteSpace(req.SystemName)   ? null : req.SystemName.Trim();
        client.PrimaryColor = string.IsNullOrWhiteSpace(req.PrimaryColor) ? null : req.PrimaryColor.ToUpperInvariant();
        client.LinkColor    = string.IsNullOrWhiteSpace(req.LinkColor)    ? null : req.LinkColor.ToUpperInvariant();

        _db.AuditEvents.Add(MakeAudit("client", id, id, "branding_updated",
            $"Branding updated for \"{client.Name}\"."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("clients/{id:long}/branding/logo")]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2 MB
    public async Task<ActionResult<ClientBrandingDto>> UploadLogo(
        long id, IFormFile logo, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        if (logo is null || logo.Length == 0) return BadRequest("No file provided.");
        if (logo.Length > 2 * 1024 * 1024)   return BadRequest("Logo must be under 2 MB.");

        var allowed = new[] { "image/png", "image/jpeg", "image/webp", "image/svg+xml" };
        if (!allowed.Contains(logo.ContentType.ToLowerInvariant()))
            return BadRequest("Logo must be a PNG, JPEG, WebP, or SVG image.");

        var key = $"logos/{id}";
        await using var stream = logo.OpenReadStream();
        await _storage.UploadAsync(key, stream, logo.ContentType, ct);

        client.LogoStorageKey = key;
        _db.AuditEvents.Add(MakeAudit("client", id, id, "branding_updated",
            $"Logo updated for \"{client.Name}\"."));
        await _db.SaveChangesAsync(ct);

        return Ok(await BuildBrandingDtoAsync(client, ct));
    }

    [HttpDelete("clients/{id:long}/branding/logo")]
    public async Task<IActionResult> DeleteLogo(long id, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        if (client.LogoStorageKey is not null)
        {
            try { await _storage.DeleteAsync(client.LogoStorageKey, ct); } catch { /* non-fatal */ }
            client.LogoStorageKey = null;
        }

        _db.AuditEvents.Add(MakeAudit("client", id, id, "branding_updated",
            $"Logo removed for \"{client.Name}\"."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ClientBrandingDto> BuildBrandingDtoAsync(
        ImperaOps.Domain.Entities.Client client, CancellationToken ct)
    {
        string? logoUrl = null;
        if (client.LogoStorageKey is not null)
        {
            try { logoUrl = await _storage.GetPresignedUrlAsync(client.LogoStorageKey, TimeSpan.FromHours(2)); }
            catch { /* return null if storage unavailable */ }
        }
        return new ClientBrandingDto(client.SystemName, client.PrimaryColor, client.LinkColor, logoUrl);
    }

    // ── Client Inbound Email ─────────────────────────────────────────────

    [HttpGet("clients/{id:long}/inbound-email")]
    public async Task<ActionResult<ClientInboundEmailDto>> GetInboundEmail(
        long id, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();
        return Ok(await BuildInboundEmailDtoAsync(client, ct));
    }

    [HttpPut("clients/{id:long}/inbound-email")]
    public async Task<IActionResult> UpdateInboundEmail(
        long id, [FromBody] UpdateClientInboundEmailRequest req, CancellationToken ct)
    {
        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound();

        // Validate uniqueness of slug (ignore current client)
        if (!string.IsNullOrWhiteSpace(req.InboundEmailSlug))
        {
            var slug = req.InboundEmailSlug.Trim().ToLowerInvariant();
            var collision = await _db.Clients.AnyAsync(
                c => c.InboundEmailSlug == slug && c.Id != id, ct);
            if (collision) return BadRequest("Inbound email slug is already in use.");
            client.InboundEmailSlug = slug;
        }
        else
        {
            client.InboundEmailSlug = null;
        }

        client.DefaultInboundEventTypeId       = req.DefaultInboundEventTypeId;
        client.DefaultInboundWorkflowStatusId  = req.DefaultInboundWorkflowStatusId;

        _db.AuditEvents.Add(MakeAudit("client", id, id, "inbound_email_updated",
            $"Inbound email settings updated for \"{client.Name}\"."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ClientInboundEmailDto> BuildInboundEmailDtoAsync(
        ImperaOps.Domain.Entities.Client client, CancellationToken ct)
    {
        var inboundDomain = _config["App:InboundDomain"] ?? "";
        var slug = client.InboundEmailSlug;
        var address = (!string.IsNullOrWhiteSpace(slug) && !string.IsNullOrWhiteSpace(inboundDomain))
            ? $"{slug}@{inboundDomain}" : null;

        var eventTypes = await _db.EventTypes
            .AsNoTracking()
            .Where(t => (t.ClientId == 0 || t.ClientId == client.Id) && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new InboundEventTypeOption(t.Id, t.Name))
            .ToListAsync(ct);

        var statuses = await _db.WorkflowStatuses
            .AsNoTracking()
            .Where(s => (s.ClientId == 0 || s.ClientId == client.Id) && !s.IsClosed && s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new InboundWorkflowStatusOption(s.Id, s.Name))
            .ToListAsync(ct);

        return new ClientInboundEmailDto(
            slug, address,
            client.DefaultInboundEventTypeId,
            client.DefaultInboundWorkflowStatusId,
            eventTypes, statuses);
    }

    // ── Event Templates ──────────────────────────────────────────────────

    /// <summary>Returns the list of available built-in event templates.</summary>
    [HttpGet("/api/v1/event-templates")]
    public IActionResult GetEventTemplates()
    {
        var result = TemplateLibrary.All.Values
            .OrderBy(t => t.Name)
            .Select(t => new EventTemplateListItemDto(
                t.Id, t.Name, t.Description, t.Industry,
                t.EventTypes.Count, t.WorkflowStatuses.Count, t.CustomFields.Count))
            .ToList();
        return Ok(result);
    }

    /// <summary>Applies a built-in template to an existing client.</summary>
    [HttpPost("clients/{id:long}/apply-template/{templateId}")]
    public async Task<IActionResult> ApplyTemplate(
        long id, string templateId, CancellationToken ct)
    {
        if (!TemplateLibrary.All.TryGetValue(templateId, out var template))
            return NotFound("Template not found.");

        var client = await _db.Clients.FindAsync([id], ct);
        if (client is null) return NotFound("Client not found.");

        var applied = ParseTemplateIds(client.AppliedTemplateIds);
        if (applied.Contains(templateId))
            return Conflict($"Template \"{template.Name}\" has already been applied to this client.");

        await ApplyTemplateToClientAsync(id, template, ct);

        // Record the application on the client
        var updated = applied.Append(templateId).ToList();
        client.AppliedTemplateIds = JsonSerializer.Serialize(updated);

        _db.AuditEvents.Add(MakeAudit("client", id, id, "template_applied",
            $"Template \"{template.Name}\" applied."));
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private async Task ApplyTemplateToClientAsync(
        long clientId, EventTemplateDefinition template, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // ── Event types ──────────────────────────────────────────────────
        var etEntities = template.EventTypes
            .Select(et => new EventType
            {
                ClientId  = clientId,
                Name      = et.Name,
                SortOrder = et.SortOrder,
                IsSystem  = false,
                IsActive  = true,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();

        foreach (var et in etEntities) _db.EventTypes.Add(et);
        await _db.SaveChangesAsync(ct); // flush to get IDs

        // ── Workflow statuses ────────────────────────────────────────────
        var wsEntities = template.WorkflowStatuses
            .Select(ws => new WorkflowStatus
            {
                ClientId  = clientId,
                Name      = ws.Name,
                Color     = ws.Color,
                IsClosed  = ws.IsClosed,
                SortOrder = ws.SortOrder,
                IsSystem  = false,
                IsActive  = true,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();

        foreach (var ws in wsEntities) _db.WorkflowStatuses.Add(ws);
        await _db.SaveChangesAsync(ct); // flush to get IDs

        var statusMap = template.WorkflowStatuses
            .Zip(wsEntities, (t, e) => (t.Key, e.Id))
            .ToDictionary(x => x.Key, x => x.Id);

        // ── Transitions ──────────────────────────────────────────────────
        foreach (var wt in template.WorkflowTransitions)
        {
            _db.WorkflowTransitions.Add(new WorkflowTransition
            {
                ClientId     = clientId,
                FromStatusId = wt.FromStatusKey != null ? statusMap[wt.FromStatusKey] : null,
                ToStatusId   = statusMap[wt.ToStatusKey],
                EventTypeId  = null,
                IsDefault    = wt.IsDefault,
                Label        = wt.Label,
                CreatedAt    = now,
            });
        }

        // ── Custom fields ────────────────────────────────────────────────
        foreach (var cf in template.CustomFields)
        {
            _db.CustomFields.Add(new CustomField
            {
                ClientId  = clientId,
                Name      = cf.Name,
                DataType  = cf.DataType,
                IsRequired = cf.IsRequired,
                SortOrder = cf.SortOrder,
                IsActive  = true,
                Options   = cf.Options,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static IReadOnlyList<string> ParseTemplateIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-");
        return slug.Trim('-');
    }

    // ── Users — update & toggle ──────────────────────────────────────────

    [HttpPut("users/{id:long}")]
    public async Task<IActionResult> UpdateUser(
        long id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Email))       return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(req.DisplayName)) return BadRequest("Display name is required.");

        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct))
            return Conflict("Another user with that email already exists.");

        var oldEmail     = user.Email;
        var oldName      = user.DisplayName;
        var oldActive    = user.IsActive;
        var oldSuperAdmin = user.IsSuperAdmin;

        user.Email        = email;
        user.DisplayName  = req.DisplayName.Trim();
        user.IsActive     = req.IsActive;
        user.IsSuperAdmin = req.IsSuperAdmin;

        var changes = new List<string>();
        if (oldEmail     != user.Email)        changes.Add($"email → \"{user.Email}\"");
        if (oldName      != user.DisplayName)  changes.Add($"name → \"{user.DisplayName}\"");
        if (oldActive    != user.IsActive)     changes.Add(user.IsActive ? "activated" : "deactivated");
        if (oldSuperAdmin != user.IsSuperAdmin) changes.Add(user.IsSuperAdmin ? "promoted to super admin" : "demoted from super admin");

        var detail = changes.Count > 0 ? string.Join("; ", changes) + "." : "No changes.";
        _db.AuditEvents.Add(MakeAudit("user", id, req.AuditClientId ?? 0, "updated",
            $"User \"{user.DisplayName}\" ({user.Email}): {detail}"));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("users/{id:long}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(long id, [FromQuery] long? clientId, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        user.IsActive = !user.IsActive;
        _db.AuditEvents.Add(MakeAudit("user", id, clientId ?? 0, "toggled",
            user.IsActive
                ? $"User \"{user.DisplayName}\" ({user.Email}) activated."
                : $"User \"{user.DisplayName}\" ({user.Email}) deactivated."));
        await _db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPut("users/{id:long}/password")]
    public async Task<IActionResult> ChangePassword(
        long id, [FromBody] ChangePasswordRequest req, [FromQuery] long? clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        _db.AuditEvents.Add(MakeAudit("user", id, clientId ?? 0, "password_changed",
            $"Password changed for \"{user.DisplayName}\" ({user.Email}) by admin."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{id:long}/totp/disable")]
    public async Task<IActionResult> DisableTotp(long id, [FromQuery] long? clientId, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        if (!user.IsTotpEnabled)
            return BadRequest("Two-factor authentication is not enabled for this user.");

        user.TotpSecret    = null;
        user.IsTotpEnabled = false;
        _db.AuditEvents.Add(MakeAudit("user", id, clientId ?? 0, "totp_disabled",
            $"TOTP two-factor authentication disabled for \"{user.DisplayName}\" ({user.Email}) by admin."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Users — client access ────────────────────────────────────────────

    [HttpGet("users/{id:long}/clients")]
    public async Task<ActionResult<IReadOnlyList<UserClientAccessDto>>> GetUserClients(
        long id, CancellationToken ct)
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

    [HttpPost("users/{id:long}/clients")]
    public async Task<IActionResult> GrantClientAccess(
        long id, [FromBody] GrantClientAccessRequest req, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == id, ct))              return NotFound("User not found.");
        if (!await _db.Clients.AnyAsync(c => c.Id == req.ClientId, ct))  return NotFound("Client not found.");

        var existing = await _db.UserClientAccess
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
            _db.UserClientAccess.Add(new UserClientAccess
            {
                UserId    = id,
                ClientId  = req.ClientId,
                Role      = req.Role,
                GrantedAt = DateTimeOffset.UtcNow,
            });
        }

        _db.AuditEvents.Add(MakeAudit("user_client_access", id, req.ClientId, "access_granted",
            $"Access granted as {req.Role}."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("users/{id:long}/clients/{clientId:long}")]
    public async Task<IActionResult> RevokeClientAccess(
        long id, long clientId, CancellationToken ct)
    {
        var access = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.UserId == id && a.ClientId == clientId, ct);

        if (access is null) return NotFound();

        access.DeletedAt = DateTimeOffset.UtcNow;
        _db.AuditEvents.Add(MakeAudit("user_client_access", id, clientId, "access_revoked",
            "Access revoked."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Clients — user list & role ───────────────────────────────────────

    [HttpGet("clients/{id:long}/users")]
    public async Task<ActionResult<IReadOnlyList<AdminClientUserDto>>> GetClientUsers(
        long id, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == id, ct)) return NotFound();

        var users = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.ClientId == id)
            .Join(_db.Users, a => a.UserId, u => u.Id, (a, u) => new { a, u })
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
        var access = await _db.UserClientAccess
            .FirstOrDefaultAsync(a => a.ClientId == id && a.UserId == userId, ct);
        if (access is null) return NotFound();

        var oldRole  = access.Role;
        access.Role  = req.Role;
        _db.AuditEvents.Add(MakeAudit("user_client_access", userId, id, "role_changed",
            $"Role changed from {oldRole} to {req.Role}."));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── SLA Rules ────────────────────────────────────────────────────────

    [HttpGet("clients/{id:long}/sla-rules")]
    public async Task<ActionResult<IReadOnlyList<SlaRuleDto>>> GetSlaRules(long id, CancellationToken ct)
    {
        var eventTypes = await _db.EventTypes
            .AsNoTracking()
            .Where(t => t.ClientId == 0 || t.ClientId == id)
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var rules = await _db.SlaRules
            .AsNoTracking()
            .Where(r => r.ClientId == id)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        return Ok(rules.Select(r => new SlaRuleDto(
            r.Id,
            r.EventTypeId,
            r.EventTypeId.HasValue ? (eventTypes.TryGetValue(r.EventTypeId.Value, out var n) ? n : null) : null,
            r.Name,
            r.InvestigationHours,
            r.ClosureHours)).ToList());
    }

    [HttpPost("clients/{id:long}/sla-rules")]
    public async Task<ActionResult<SlaRuleDto>> CreateSlaRule(
        long id, [FromBody] CreateSlaRuleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        if (!await _db.Clients.AnyAsync(c => c.Id == id, ct)) return NotFound();

        var rule = new ImperaOps.Domain.Entities.SlaRule
        {
            ClientId           = id,
            EventTypeId        = req.EventTypeId,
            Name               = req.Name.Trim(),
            InvestigationHours = req.InvestigationHours,
            ClosureHours       = req.ClosureHours,
            CreatedAt          = DateTimeOffset.UtcNow,
        };

        _db.SlaRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return Ok(new SlaRuleDto(rule.Id, rule.EventTypeId, null, rule.Name, rule.InvestigationHours, rule.ClosureHours));
    }

    [HttpPut("clients/{id:long}/sla-rules/{ruleId:long}")]
    public async Task<IActionResult> UpdateSlaRule(
        long id, long ruleId, [FromBody] CreateSlaRuleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");

        var rule = await _db.SlaRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ClientId == id, ct);
        if (rule is null) return NotFound();

        rule.EventTypeId        = req.EventTypeId;
        rule.Name               = req.Name.Trim();
        rule.InvestigationHours = req.InvestigationHours;
        rule.ClosureHours       = req.ClosureHours;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("clients/{id:long}/sla-rules/{ruleId:long}")]
    public async Task<IActionResult> DeleteSlaRule(long id, long ruleId, CancellationToken ct)
    {
        var rule = await _db.SlaRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.ClientId == id, ct);
        if (rule is null) return NotFound();

        rule.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Admin audit log ──────────────────────────────────────────────────

    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AdminAuditEventDto>>> GetAdminAudit(
        [FromQuery] long? clientId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.AuditEvents
            .AsNoTracking()
            .Where(a => a.EntityType == "client"
                     || a.EntityType == "user"
                     || a.EntityType == "user_client_access");

        if (clientId.HasValue && clientId.Value > 0)
            query = query.Where(a => a.ClientId == clientId.Value);

        query = query.OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(ct);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Batch-load client names for the IDs that appear in this page
        var distinctClientIds = rawItems
            .Where(a => a.ClientId > 0)
            .Select(a => a.ClientId)
            .Distinct()
            .ToList();

        var clientNames = distinctClientIds.Count > 0
            ? await _db.Clients
                .IgnoreQueryFilters()
                .Where(c => distinctClientIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<long, string>();

        var items = rawItems.Select(a => new AdminAuditEventDto(
            a.Id, a.ClientId,
            a.ClientId > 0 && clientNames.TryGetValue(a.ClientId, out var cName) ? cName : null,
            a.EntityType, a.EntityId, a.EventType,
            a.UserId, a.UserDisplayName, a.Body, a.CreatedAt))
            .ToList();

        return Ok(new PagedResult<AdminAuditEventDto>(items, total, page, pageSize));
    }
}
