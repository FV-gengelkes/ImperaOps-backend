using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ImperaOps.Api.Controllers;

public record CreateWebhookRequest(string Name, string Url, string? Secret, string[] EventTypes, bool IsActive);
public record UpdateWebhookRequest(string Name, string Url, string? Secret, string[] EventTypes, bool IsActive);

public record WebhookDto(
    long Id, long ClientId, string Name, string Url, string? Secret,
    string[] EventTypes, bool IsActive, DateTimeOffset CreatedAt);

[ApiController]
[Route("api/v1/clients/{clientId:long}/webhooks")]
public sealed class ClientWebhooksController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public ClientWebhooksController(ImperaOpsDbContext db) => _db = db;

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WebhookDto>>> List(
        long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var items = await _db.ClientWebhooks
            .AsNoTracking()
            .Where(w => w.ClientId == clientId)
            .OrderBy(w => w.Id)
            .ToListAsync(ct);

        return Ok(items.Select(ToDto).ToList());
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<WebhookDto>> Create(
        long clientId, [FromBody] CreateWebhookRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(req.Name))  throw new ValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(req.Url))   throw new ValidationException("Url is required.");

        var now = DateTimeOffset.UtcNow;
        var webhook = new ClientWebhook
        {
            ClientId   = clientId,
            Name       = req.Name.Trim(),
            Url        = req.Url.Trim(),
            Secret     = string.IsNullOrWhiteSpace(req.Secret) ? null : req.Secret.Trim(),
            EventTypes = JsonSerializer.Serialize(req.EventTypes ?? []),
            IsActive   = req.IsActive,
            CreatedAt  = now,
            UpdatedAt  = now,
        };

        _db.ClientWebhooks.Add(webhook);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(webhook));
    }

    [Authorize]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<WebhookDto>> Update(
        long clientId, long id, [FromBody] UpdateWebhookRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var webhook = await _db.ClientWebhooks.FirstOrDefaultAsync(w => w.Id == id && w.ClientId == clientId, ct);
        if (webhook is null) throw new NotFoundException();

        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(req.Url))  throw new ValidationException("Url is required.");

        webhook.Name       = req.Name.Trim();
        webhook.Url        = req.Url.Trim();
        webhook.Secret     = string.IsNullOrWhiteSpace(req.Secret) ? null : req.Secret.Trim();
        webhook.EventTypes = JsonSerializer.Serialize(req.EventTypes ?? []);
        webhook.IsActive   = req.IsActive;
        webhook.UpdatedAt  = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(webhook));
    }

    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        long clientId, long id, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var webhook = await _db.ClientWebhooks.FirstOrDefaultAsync(w => w.Id == id && w.ClientId == clientId, ct);
        if (webhook is null) throw new NotFoundException();

        webhook.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static WebhookDto ToDto(ClientWebhook w)
    {
        var types = JsonSerializer.Deserialize<string[]>(w.EventTypes) ?? [];
        return new WebhookDto(w.Id, w.ClientId, w.Name, w.Url, w.Secret, types, w.IsActive, w.CreatedAt);
    }
}
