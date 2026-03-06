using System.Text.RegularExpressions;
using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public sealed class WebhookController : ControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly ICounterService _counter;
    private readonly IEventRepository _repo;
    private readonly IConfiguration _config;

    public WebhookController(
        ImperaOpsDbContext db,
        ICounterService counter,
        IEventRepository repo,
        IConfiguration config)
    {
        _db      = db;
        _counter = counter;
        _repo    = repo;
        _config  = config;
    }

    // ── POST /api/v1/webhooks/email ────────────────────────────────────────────
    // Resend inbound email webhook — no auth, relies on inbound slug matching.

    [HttpPost("email")]
    public async Task<IActionResult> InboundEmail(
        [FromBody] ResendInboundPayload payload,
        CancellationToken ct)
    {
        if (payload is null) return BadRequest("Empty payload.");

        var inboundDomain = _config["App:InboundDomain"] ?? "";

        // ── 1. Extract inbound slug from To addresses ──────────────────────────
        string? inboundSlug = null;
        foreach (var address in payload.To ?? [])
        {
            var local = ExtractLocalPart(address, inboundDomain);
            if (local is not null)
            {
                inboundSlug = local;
                break;
            }
        }

        if (inboundSlug is null)
            return Ok(new { skipped = true, reason = "No matching inbound address." });

        // ── 2. Look up client by InboundEmailSlug ──────────────────────────────
        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.InboundEmailSlug == inboundSlug && c.IsActive, ct);

        if (client is null)
            return Ok(new { skipped = true, reason = "No client found for inbound slug." });

        // ── 3. Resolve default event type ─────────────────────────────────────
        long eventTypeId;
        if (client.DefaultInboundEventTypeId.HasValue)
        {
            var typeValid = await _db.EventTypes.AnyAsync(
                t => t.Id == client.DefaultInboundEventTypeId.Value &&
                     (t.ClientId == 0 || t.ClientId == client.Id) &&
                     t.IsActive, ct);
            eventTypeId = typeValid
                ? client.DefaultInboundEventTypeId.Value
                : await FirstActiveEventTypeId(client.Id, ct);
        }
        else
        {
            eventTypeId = await FirstActiveEventTypeId(client.Id, ct);
        }

        if (eventTypeId == 0)
            return Ok(new { skipped = true, reason = "No active event type configured." });

        // ── 4. Resolve default workflow status ─────────────────────────────────
        long statusId;
        if (client.DefaultInboundWorkflowStatusId.HasValue)
        {
            var statusValid = await _db.WorkflowStatuses.AnyAsync(
                s => s.Id == client.DefaultInboundWorkflowStatusId.Value &&
                     (s.ClientId == 0 || s.ClientId == client.Id) &&
                     !s.IsClosed && s.IsActive, ct);
            statusId = statusValid
                ? client.DefaultInboundWorkflowStatusId.Value
                : await FirstOpenStatusId(client.Id, ct);
        }
        else
        {
            statusId = await FirstOpenStatusId(client.Id, ct);
        }

        if (statusId == 0)
            return Ok(new { skipped = true, reason = "No open workflow status configured." });

        // ── 5. Build event fields ──────────────────────────────────────────────
        var subject = payload.Subject?.Trim();
        if (string.IsNullOrWhiteSpace(subject)) subject = "(No Subject)";

        var body = payload.Text?.Trim() ?? payload.Html?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(body)) body = "(No body)";

        // Strip HTML tags if only HTML was provided
        if (string.IsNullOrWhiteSpace(payload.Text) && !string.IsNullOrWhiteSpace(payload.Html))
            body = Regex.Replace(body, "<[^>]*>", " ").Trim();

        // Parse reporter name + contact from From header
        var (reporterName, reporterContact) = ParseFrom(payload.From);

        var now = DateTimeOffset.UtcNow;
        var refNumber = await _counter.AllocateAsync(client.Id, "event", ct);
        var publicId  = $"EVT-{refNumber:D4}";

        var ev = new Event
        {
            ClientId                = client.Id,
            PublicId                = publicId,
            EventTypeId             = eventTypeId,
            WorkflowStatusId        = statusId,
            Title                   = subject.Length > 500 ? subject[..500] : subject,
            OccurredAt              = now,
            Location                = "",
            Description             = body,
            ReportedByUserId        = null,
            ExternalReporterName    = reporterName,
            ExternalReporterContact = reporterContact,
            OwnerUserId             = null,
            ReferenceNumber         = refNumber,
            CreatedAt               = now,
            UpdatedAt               = now,
        };

        var eventId = await _repo.CreateAsync(ev, ct);

        _db.AuditEvents.Add(new AuditEvent
        {
            ClientId        = client.Id,
            EntityType      = "event",
            EntityId        = eventId,
            EventType       = "created",
            UserId          = null,
            UserDisplayName = reporterName ?? "Email",
            Body            = "Event created via inbound email.",
            CreatedAt       = now,
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new { publicId });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Extracts the local part of an address if it ends with @{domain}.</summary>
    private static string? ExtractLocalPart(string address, string domain)
    {
        // address may be "Name <local@domain>" or "local@domain"
        var match = Regex.Match(address, @"<([^>]+)>|(\S+@\S+)");
        var email = match.Success
            ? (match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : match.Groups[2].Value)
            : address.Trim();

        if (!email.Contains('@')) return null;
        var parts = email.Split('@', 2);
        if (!string.IsNullOrWhiteSpace(domain) &&
            !parts[1].Equals(domain, StringComparison.OrdinalIgnoreCase))
            return null;

        return parts[0].Trim().ToLowerInvariant();
    }

    /// <summary>Parses "Display Name &lt;email@domain&gt;" or bare email address.</summary>
    private static (string? name, string? contact) ParseFrom(string? from)
    {
        if (string.IsNullOrWhiteSpace(from)) return (null, null);

        var angleMatch = Regex.Match(from, @"^(.*?)<([^>]+)>");
        if (angleMatch.Success)
        {
            var name    = angleMatch.Groups[1].Value.Trim().Trim('"');
            var email   = angleMatch.Groups[2].Value.Trim();
            return (string.IsNullOrWhiteSpace(name) ? null : name, email);
        }

        // Bare email
        return (null, from.Trim());
    }

    private async Task<long> FirstActiveEventTypeId(long clientId, CancellationToken ct) =>
        await _db.EventTypes
            .AsNoTracking()
            .Where(t => (t.ClientId == 0 || t.ClientId == clientId) && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

    private async Task<long> FirstOpenStatusId(long clientId, CancellationToken ct) =>
        await _db.WorkflowStatuses
            .AsNoTracking()
            .Where(s => (s.ClientId == 0 || s.ClientId == clientId) && !s.IsClosed && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>Resend inbound email webhook payload.</summary>
public sealed class ResendInboundPayload
{
    public string? From    { get; set; }
    public List<string>? To { get; set; }
    public string? Subject { get; set; }
    /// <summary>Plain-text body (preferred).</summary>
    public string? Text    { get; set; }
    /// <summary>HTML body (fallback if Text is absent).</summary>
    public string? Html    { get; set; }
}
