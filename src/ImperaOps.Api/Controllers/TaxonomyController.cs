using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:long}/taxonomy")]
[Authorize]
public sealed class TaxonomyController : ScopedControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public TaxonomyController(ImperaOpsDbContext db) => _db = db;

    // ── Root Cause Taxonomy ──────────────────────────────────────────────────

    [HttpGet("root-cause")]
    public async Task<ActionResult<IReadOnlyList<RootCauseTaxonomyItemDto>>> GetRootCauses(
        long clientId, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();

        var items = await _db.RootCauseTaxonomyItems
            .AsNoTracking()
            .Where(r => r.ClientId == clientId)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .Select(r => new RootCauseTaxonomyItemDto(r.Id, r.Name, r.SortOrder))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("root-cause")]
    public async Task<ActionResult<RootCauseTaxonomyItemDto>> CreateRootCause(
        long clientId, [FromBody] CreateRootCauseRequest req, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");

        var maxSort = await _db.RootCauseTaxonomyItems
            .Where(r => r.ClientId == clientId)
            .Select(r => (int?)r.SortOrder)
            .MaxAsync(ct) ?? 0;

        var item = new RootCauseTaxonomyItem
        {
            ClientId  = clientId,
            Name      = req.Name.Trim(),
            SortOrder = maxSort + 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.RootCauseTaxonomyItems.Add(item);
        await _db.SaveChangesAsync(ct);

        return Ok(new RootCauseTaxonomyItemDto(item.Id, item.Name, item.SortOrder));
    }

    [HttpPut("root-cause/{id:long}")]
    public async Task<IActionResult> UpdateRootCause(
        long clientId, long id, [FromBody] CreateRootCauseRequest req, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");

        var item = await _db.RootCauseTaxonomyItems
            .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId, ct);
        if (item is null) return NotFound();

        item.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("root-cause/{id:long}")]
    public async Task<IActionResult> DeleteRootCause(
        long clientId, long id, CancellationToken ct)
    {
        if (!HasClientAccess(clientId)) return NotFound();
        if (!await IsAdminOfClientAsync(_db, clientId, User, ct)) return Forbid();

        var item = await _db.RootCauseTaxonomyItems
            .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId, ct);
        if (item is null) return NotFound();

        // Check if in use
        var inUse = await _db.Events.AnyAsync(e => e.RootCauseId == id, ct);
        if (inUse) return Conflict("This root cause is referenced by one or more events and cannot be deleted.");

        item.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record RootCauseTaxonomyItemDto(long Id, string Name, int SortOrder);
public sealed record CreateRootCauseRequest(string Name);
