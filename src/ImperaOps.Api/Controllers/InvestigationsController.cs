using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/events/{publicId}/investigation")]
[Authorize]
public sealed class InvestigationsController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public InvestigationsController(ImperaOpsDbContext db) => _db = db;

    private async Task<(Event? ev, long clientId)> ResolveEvent(string publicId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == publicId, ct);
        return (ev, ev?.ClientId ?? 0);
    }

    // GET api/v1/events/{publicId}/investigation
    [HttpGet]
    public async Task<IActionResult> Get(string publicId, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var inv = await _db.Investigations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);
        if (inv == null) return Ok((object?)null);

        var leadName = inv.LeadInvestigatorUserId.HasValue
            ? (await _db.Users.AsNoTracking().Where(u => u.Id == inv.LeadInvestigatorUserId.Value).Select(u => u.DisplayName).FirstOrDefaultAsync(ct))
            : null;

        return Ok(ToDto(inv, leadName));
    }

    // POST api/v1/events/{publicId}/investigation
    [HttpPost]
    public async Task<IActionResult> Start(string publicId, [FromBody] CreateInvestigationRequest req, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var exists = await _db.Investigations.AnyAsync(i => i.EventId == ev.Id, ct);
        if (exists) throw new ConflictException("Investigation already exists for this event.");

        var now = DateTimeOffset.UtcNow;
        var inv = new Investigation
        {
            ClientId = clientId,
            EventId = ev.Id,
            Status = "draft",
            LeadInvestigatorUserId = req.LeadInvestigatorUserId,
            CreatedByUserId = CurrentUserId(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Investigations.Add(inv);

        Audit.Record("event", ev.Id, clientId, "investigation_started",
            $"Investigation started for {ev.PublicId}");

        await _db.SaveChangesAsync(ct);
        return Ok(new { inv.Id });
    }

    // PUT api/v1/events/{publicId}/investigation
    [HttpPut]
    public async Task<IActionResult> Update(string publicId, [FromBody] UpdateInvestigationRequest req, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);

        var isManager = await IsManagerOrAboveAsync(_db, clientId, User, ct);
        var isInvestigator = !isManager && await IsInvestigatorOrAboveAsync(_db, clientId, User, ct);
        if (!isManager && !isInvestigator) throw new ForbiddenException();

        var inv = await _db.Investigations.FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);
        if (inv == null) throw new NotFoundException();

        // Investigators can only update if they are the lead
        if (isInvestigator && inv.LeadInvestigatorUserId != CurrentUserId())
            throw new ForbiddenException();

        var now = DateTimeOffset.UtcNow;

        if (req.Status != null && req.Status != inv.Status)
        {
            if (!IsValidTransition(inv.Status, req.Status))
                throw new ValidationException($"Invalid status transition: {inv.Status} -> {req.Status}");

            inv.Status = req.Status;
            if (req.Status == "in_progress" && inv.StartedAt == null)
                inv.StartedAt = now;
            if (req.Status == "completed")
                inv.CompletedAt = now;

            if (req.Status == "completed")
            {
                Audit.Record("event", ev.Id, clientId, "investigation_completed",
                    $"Investigation completed for {ev.PublicId}");
            }
            else
            {
                Audit.Record("event", ev.Id, clientId, "investigation_updated",
                    $"Investigation status changed to {req.Status} for {ev.PublicId}");
            }
        }

        if (req.Summary != null) inv.Summary = req.Summary;
        if (req.RootCauseAnalysis != null) inv.RootCauseAnalysis = req.RootCauseAnalysis;
        if (req.CorrectiveActions != null) inv.CorrectiveActions = req.CorrectiveActions;
        if (req.LeadInvestigatorUserId.HasValue) inv.LeadInvestigatorUserId = req.LeadInvestigatorUserId;

        inv.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Witnesses ────────────────────────────────────────────────────────────────

    [HttpGet("witnesses")]
    public async Task<IActionResult> GetWitnesses(string publicId, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var inv = await _db.Investigations.AsNoTracking().FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);
        if (inv == null) return Ok(Array.Empty<WitnessDto>());

        var witnesses = await _db.InvestigationWitnesses.AsNoTracking()
            .Where(w => w.InvestigationId == inv.Id)
            .OrderBy(w => w.SortOrder)
            .ToListAsync(ct);

        return Ok(witnesses.Select(w => new WitnessDto(
            w.Id, w.InvestigationId, w.WitnessName, w.WitnessContact,
            w.Statement, w.StatementDate?.ToString("o"), w.SortOrder, w.CreatedAt.ToString("o"))));
    }

    [HttpPost("witnesses")]
    public async Task<IActionResult> AddWitness(string publicId, [FromBody] CreateWitnessRequest req, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var inv = await _db.Investigations.FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);
        if (inv == null) throw new NotFoundException("No investigation for this event.");

        var maxSort = await _db.InvestigationWitnesses
            .Where(w => w.InvestigationId == inv.Id)
            .MaxAsync(w => (int?)w.SortOrder, ct) ?? -1;

        var now = DateTimeOffset.UtcNow;
        var w = new InvestigationWitness
        {
            InvestigationId = inv.Id,
            WitnessName = req.WitnessName.Trim(),
            WitnessContact = req.WitnessContact?.Trim(),
            Statement = req.Statement,
            StatementDate = req.StatementDate != null ? DateTimeOffset.Parse(req.StatementDate) : null,
            RecordedByUserId = CurrentUserId(),
            SortOrder = maxSort + 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.InvestigationWitnesses.Add(w);

        Audit.Record("event", ev.Id, clientId, "witness_added",
            $"Witness \"{req.WitnessName}\" added to investigation for {ev.PublicId}");

        await _db.SaveChangesAsync(ct);
        return Ok(new WitnessDto(w.Id, w.InvestigationId, w.WitnessName, w.WitnessContact,
            w.Statement, w.StatementDate?.ToString("o"), w.SortOrder, w.CreatedAt.ToString("o")));
    }

    [HttpPut("witnesses/{id:long}")]
    public async Task<IActionResult> UpdateWitness(string publicId, long id, [FromBody] UpdateWitnessRequest req, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var w = await _db.InvestigationWitnesses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w == null) throw new NotFoundException();

        w.WitnessName = req.WitnessName.Trim();
        w.WitnessContact = req.WitnessContact?.Trim();
        w.Statement = req.Statement;
        w.StatementDate = req.StatementDate != null ? DateTimeOffset.Parse(req.StatementDate) : null;
        w.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("witnesses/{id:long}")]
    public async Task<IActionResult> DeleteWitness(string publicId, long id, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var w = await _db.InvestigationWitnesses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w == null) throw new NotFoundException();

        w.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Evidence ─────────────────────────────────────────────────────────────────

    [HttpGet("evidence")]
    public async Task<IActionResult> GetEvidence(string publicId, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var inv = await _db.Investigations.AsNoTracking().FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);
        if (inv == null) return Ok(Array.Empty<EvidenceDto>());

        var items = await _db.InvestigationEvidence.AsNoTracking()
            .Where(e => e.InvestigationId == inv.Id)
            .OrderBy(e => e.SortOrder)
            .ToListAsync(ct);

        return Ok(items.Select(e => new EvidenceDto(
            e.Id, e.InvestigationId, e.Title, e.Description, e.EvidenceType,
            e.AttachmentId, e.CollectedAt?.ToString("o"), e.SortOrder, e.CreatedAt.ToString("o"))));
    }

    [HttpPost("evidence")]
    public async Task<IActionResult> AddEvidence(string publicId, [FromBody] CreateEvidenceRequest req, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var inv = await _db.Investigations.FirstOrDefaultAsync(i => i.EventId == ev.Id, ct);
        if (inv == null) throw new NotFoundException("No investigation for this event.");

        var maxSort = await _db.InvestigationEvidence
            .Where(e => e.InvestigationId == inv.Id)
            .MaxAsync(e => (int?)e.SortOrder, ct) ?? -1;

        var now = DateTimeOffset.UtcNow;
        var item = new InvestigationEvidence
        {
            InvestigationId = inv.Id,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim(),
            EvidenceType = req.EvidenceType,
            AttachmentId = req.AttachmentId,
            CollectedByUserId = CurrentUserId(),
            CollectedAt = req.CollectedAt != null ? DateTimeOffset.Parse(req.CollectedAt) : null,
            SortOrder = maxSort + 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.InvestigationEvidence.Add(item);

        Audit.Record("event", ev.Id, clientId, "evidence_added",
            $"Evidence \"{req.Title}\" added to investigation for {ev.PublicId}");

        await _db.SaveChangesAsync(ct);
        return Ok(new EvidenceDto(item.Id, item.InvestigationId, item.Title, item.Description,
            item.EvidenceType, item.AttachmentId, item.CollectedAt?.ToString("o"), item.SortOrder, item.CreatedAt.ToString("o")));
    }

    [HttpPut("evidence/{id:long}")]
    public async Task<IActionResult> UpdateEvidence(string publicId, long id, [FromBody] UpdateEvidenceRequest req, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsInvestigatorOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var item = await _db.InvestigationEvidence.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item == null) throw new NotFoundException();

        item.Title = req.Title.Trim();
        item.Description = req.Description?.Trim();
        item.EvidenceType = req.EvidenceType;
        item.AttachmentId = req.AttachmentId;
        item.CollectedAt = req.CollectedAt != null ? DateTimeOffset.Parse(req.CollectedAt) : null;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("evidence/{id:long}")]
    public async Task<IActionResult> DeleteEvidence(string publicId, long id, CancellationToken ct)
    {
        var (ev, clientId) = await ResolveEvent(publicId, ct);
        if (ev == null) throw new NotFoundException();
        RequireClientAccess(clientId);
        if (!await IsManagerOrAboveAsync(_db, clientId, User, ct)) throw new ForbiddenException();

        var item = await _db.InvestigationEvidence.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item == null) throw new NotFoundException();

        item.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static bool IsValidTransition(string from, string to) => (from, to) switch
    {
        ("draft", "in_progress") => true,
        ("in_progress", "review") => true,
        ("review", "completed") => true,
        ("completed", "in_progress") => true,
        ("review", "in_progress") => true,
        _ => false,
    };

    private static InvestigationDto ToDto(Investigation inv, string? leadName) => new(
        inv.Id, inv.ClientId, inv.EventId, inv.Status,
        inv.Summary, inv.RootCauseAnalysis, inv.CorrectiveActions,
        inv.LeadInvestigatorUserId, leadName,
        inv.StartedAt?.ToString("o"), inv.CompletedAt?.ToString("o"),
        inv.CreatedAt.ToString("o"), inv.UpdatedAt.ToString("o"));
}
