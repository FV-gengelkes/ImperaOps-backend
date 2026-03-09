using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "SuperAdmin")]
public sealed class AdminBrandingController(
    ImperaOpsDbContext db,
    IStorageService storage,
    IConfiguration config) : ScopedControllerBase
{
    // ── Branding ─────────────────────────────────────────────────────────

    [HttpGet("clients/{id:long}/branding")]
    public async Task<ActionResult<ClientBrandingDto>> GetBranding(long id, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();
        return Ok(await BuildBrandingDtoAsync(client));
    }

    [HttpPut("clients/{id:long}/branding")]
    public async Task<IActionResult> UpdateBranding(
        long id, [FromBody] UpdateBrandingRequest req, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        if (req.PrimaryColor is not null &&
            !Regex.IsMatch(req.PrimaryColor, @"^#[0-9A-Fa-f]{6}$"))
            throw new ValidationException("PrimaryColor must be a valid 6-digit hex color, e.g. #2F80ED.");

        if (req.LinkColor is not null &&
            !Regex.IsMatch(req.LinkColor, @"^#[0-9A-Fa-f]{6}$"))
            throw new ValidationException("LinkColor must be a valid 6-digit hex color, e.g. #1A5FB4.");

        client.SystemName   = string.IsNullOrWhiteSpace(req.SystemName)   ? null : req.SystemName.Trim();
        client.PrimaryColor = string.IsNullOrWhiteSpace(req.PrimaryColor) ? null : req.PrimaryColor.ToUpperInvariant();
        client.LinkColor    = string.IsNullOrWhiteSpace(req.LinkColor)    ? null : req.LinkColor.ToUpperInvariant();

        Audit.Record("client", id, id, "branding_updated",
            $"Branding updated for \"{client.Name}\".");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("clients/{id:long}/branding/logo")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<ClientBrandingDto>> UploadLogo(
        long id, IFormFile logo, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        if (logo is null || logo.Length == 0) throw new ValidationException("No file provided.");
        if (logo.Length > 2 * 1024 * 1024)   throw new ValidationException("Logo must be under 2 MB.");

        var allowed = new[] { "image/png", "image/jpeg", "image/webp", "image/svg+xml" };
        if (!allowed.Contains(logo.ContentType.ToLowerInvariant()))
            throw new ValidationException("Logo must be a PNG, JPEG, WebP, or SVG image.");

        var key = $"logos/{id}";
        await using var stream = logo.OpenReadStream();
        await storage.UploadAsync(key, stream, logo.ContentType, ct);

        client.LogoStorageKey = key;
        Audit.Record("client", id, id, "branding_updated",
            $"Logo updated for \"{client.Name}\".");
        await db.SaveChangesAsync(ct);

        return Ok(await BuildBrandingDtoAsync(client));
    }

    [HttpDelete("clients/{id:long}/branding/logo")]
    public async Task<IActionResult> DeleteLogo(long id, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        if (client.LogoStorageKey is not null)
        {
            try { await storage.DeleteAsync(client.LogoStorageKey, ct); } catch { /* non-fatal */ }
            client.LogoStorageKey = null;
        }

        Audit.Record("client", id, id, "branding_updated",
            $"Logo removed for \"{client.Name}\".");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Inbound Email ────────────────────────────────────────────────────

    [HttpGet("clients/{id:long}/inbound-email")]
    public async Task<ActionResult<ClientInboundEmailDto>> GetInboundEmail(
        long id, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();
        return Ok(await BuildInboundEmailDtoAsync(client, ct));
    }

    [HttpPut("clients/{id:long}/inbound-email")]
    public async Task<IActionResult> UpdateInboundEmail(
        long id, [FromBody] UpdateClientInboundEmailRequest req, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct);
        if (client is null) throw new NotFoundException();

        if (!string.IsNullOrWhiteSpace(req.InboundEmailSlug))
        {
            var slug = req.InboundEmailSlug.Trim().ToLowerInvariant();
            var collision = await db.Clients.AnyAsync(
                c => c.InboundEmailSlug == slug && c.Id != id, ct);
            if (collision) throw new ValidationException("Inbound email slug is already in use.");
            client.InboundEmailSlug = slug;
        }
        else
        {
            client.InboundEmailSlug = null;
        }

        client.DefaultInboundEventTypeId       = req.DefaultInboundEventTypeId;
        client.DefaultInboundWorkflowStatusId  = req.DefaultInboundWorkflowStatusId;

        Audit.Record("client", id, id, "inbound_email_updated",
            $"Inbound email settings updated for \"{client.Name}\".");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<ClientBrandingDto> BuildBrandingDtoAsync(Domain.Entities.Client client)
    {
        string? logoUrl = null;
        if (client.LogoStorageKey is not null)
        {
            try { logoUrl = await storage.GetPresignedUrlAsync(client.LogoStorageKey, TimeSpan.FromHours(2)); }
            catch { /* return null if storage unavailable */ }
        }
        return new ClientBrandingDto(client.SystemName, client.PrimaryColor, client.LinkColor, logoUrl);
    }

    private async Task<ClientInboundEmailDto> BuildInboundEmailDtoAsync(
        Domain.Entities.Client client, CancellationToken ct)
    {
        var inboundDomain = config["App:InboundDomain"] ?? "";
        var slug = client.InboundEmailSlug;
        var address = (!string.IsNullOrWhiteSpace(slug) && !string.IsNullOrWhiteSpace(inboundDomain))
            ? $"{slug}@{inboundDomain}" : null;

        var eventTypes = await db.EventTypes
            .AsNoTracking()
            .Where(t => (t.ClientId == 0 || t.ClientId == client.Id) && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new InboundEventTypeOption(t.Id, t.Name))
            .ToListAsync(ct);

        var statuses = await db.WorkflowStatuses
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
}
