using System.Text.RegularExpressions;
using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Commands;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/public/report")]
public sealed class PublicReportController : ControllerBase
{
    private readonly ImperaOpsDbContext _db;
    private readonly ICounterService _counter;
    private readonly IEventRepository _repo;
    private readonly IStorageService _storage;
    private readonly IAuditService _audit;

    public PublicReportController(
        ImperaOpsDbContext db,
        ICounterService counter,
        IEventRepository repo,
        IStorageService storage,
        IAuditService audit)
    {
        _db      = db;
        _counter = counter;
        _repo    = repo;
        _storage = storage;
        _audit   = audit;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly Regex EmailRegex = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"^\+?[\d\s\-().]{7,20}$", RegexOptions.Compiled);

    private static bool IsValidContact(string value) =>
        EmailRegex.IsMatch(value) || PhoneRegex.IsMatch(value);

    // ── GET /api/v1/public/report/{slug} ──────────────────────────────────────

    [HttpGet("{slug}")]
    public async Task<ActionResult<ReportFormConfigDto>> GetConfig(
        string slug, CancellationToken ct)
    {
        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.Status != "Inactive", ct);

        if (client is null) throw new NotFoundException("Reporting unavailable for this link.");

        var eventTypes = await _db.EventTypes
            .AsNoTracking()
            .Where(t => (t.ClientId == 0 || t.ClientId == client.Id) && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new EventTypeOption(t.Id, t.Name))
            .ToListAsync(ct);

        var defaultStatus = await _db.WorkflowStatuses
            .AsNoTracking()
            .Where(s => (s.ClientId == 0 || s.ClientId == client.Id) && !s.IsClosed && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .FirstOrDefaultAsync(ct);

        if (defaultStatus is null)
            throw new NotFoundException("No open workflow status configured for this client.");

        string? logoUrl = null;
        if (!string.IsNullOrWhiteSpace(client.LogoStorageKey))
        {
            try { logoUrl = await _storage.GetPresignedUrlAsync(client.LogoStorageKey, TimeSpan.FromHours(1)); }
            catch { /* non-fatal */ }
        }

        return Ok(new ReportFormConfigDto(
            client.Id,
            client.Name,
            client.SystemName,
            client.PrimaryColor,
            client.LinkColor,
            logoUrl,
            defaultStatus.Id,
            eventTypes));
    }

    // ── POST /api/v1/public/report/{slug} ─────────────────────────────────────

    [HttpPost("{slug}")]
    public async Task<ActionResult<PublicCreateEventResponse>> Submit(
        string slug,
        [FromBody] PublicCreateEventRequest req,
        CancellationToken ct)
    {
        // Re-validate slug → ClientId match
        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.Status != "Inactive", ct);

        if (client is null) throw new NotFoundException("Reporting unavailable for this link.");
        if (client.Id != req.ClientId) throw new ValidationException("ClientId mismatch.");

        // Validate EventType belongs to this client and is active
        var eventTypeValid = await _db.EventTypes
            .AnyAsync(t => t.Id == req.EventTypeId &&
                           (t.ClientId == 0 || t.ClientId == client.Id) &&
                           t.IsActive, ct);
        if (!eventTypeValid) throw new ValidationException("Invalid event type.");

        // Validate WorkflowStatus belongs to this client, is not closed, and is active
        var statusValid = await _db.WorkflowStatuses
            .AnyAsync(s => s.Id == req.WorkflowStatusId &&
                           (s.ClientId == 0 || s.ClientId == client.Id) &&
                           !s.IsClosed && s.IsActive, ct);
        if (!statusValid) throw new ValidationException("Invalid workflow status.");

        if (string.IsNullOrWhiteSpace(req.Title))          throw new ValidationException("Title is required.");
        if (string.IsNullOrWhiteSpace(req.Description))   throw new ValidationException("Description is required.");
        if (string.IsNullOrWhiteSpace(req.ReporterName))  throw new ValidationException("Reporter name is required.");
        if (string.IsNullOrWhiteSpace(req.ReporterContact)) throw new ValidationException("Reporter email or phone is required.");
        if (!IsValidContact(req.ReporterContact.Trim()))  throw new ValidationException("Reporter contact must be a valid email address or phone number.");

        var now = DateTimeOffset.UtcNow;
        var refNumber = await _counter.AllocateAsync(client.Id, "event", ct);
        var publicId  = $"EVT-{refNumber:D4}";

        var ev = new Event
        {
            ClientId                = client.Id,
            PublicId                = publicId,
            EventTypeId             = req.EventTypeId,
            WorkflowStatusId        = req.WorkflowStatusId,
            Title                   = req.Title.Trim(),
            OccurredAt              = req.OccurredAt ?? now,
            Location                = req.Location?.Trim() ?? "",
            Description             = req.Description.Trim(),
            ReportedByUserId        = null,
            ExternalReporterName    = req.ReporterName?.Trim(),
            ExternalReporterContact = req.ReporterContact?.Trim(),
            OwnerUserId             = null,
            ReferenceNumber         = refNumber,
            CreatedAt               = now,
            UpdatedAt               = now,
        };

        var eventId = await _repo.CreateAsync(ev, ct);

        _audit.Record("event", eventId, client.Id, "created",
            "Event submitted via public intake form.",
            null, req.ReporterName?.Trim() ?? "External");
        await _db.SaveChangesAsync(ct);

        return Ok(new PublicCreateEventResponse(publicId));
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record ReportFormConfigDto(
    long ClientId,
    string ClientName,
    string? SystemName,
    string? PrimaryColor,
    string? LinkColor,
    string? LogoUrl,
    long DefaultStatusId,
    IReadOnlyList<EventTypeOption> EventTypes);

public sealed record EventTypeOption(long Id, string Name);

public sealed record PublicCreateEventRequest(
    long ClientId,
    long EventTypeId,
    long WorkflowStatusId,
    string Title,
    string Description,
    string? Location,
    DateTimeOffset? OccurredAt,
    string? ReporterName,
    string? ReporterContact);

public sealed record PublicCreateEventResponse(string PublicId);
