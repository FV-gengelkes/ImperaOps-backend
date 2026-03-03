using FreightVis.Api.Contracts;
using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/lookups")]
public sealed class LookupsController : ControllerBase
{
    private readonly FreightVisDbContext _db;

    public LookupsController(FreightVisDbContext db) => _db = db;

    /// <summary>
    /// Returns active system defaults + client-specific rows for the given fieldKey.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentLookupDto>>> GetLookups(
        [FromQuery] Guid clientId,
        [FromQuery] string fieldKey,
        CancellationToken ct)
    {
        if (clientId == Guid.Empty)
            return BadRequest("clientId must not be the system sentinel value.");

        if (string.IsNullOrWhiteSpace(fieldKey))
            return BadRequest("fieldKey is required.");

        var rows = await _db.IncidentLookups
            .AsNoTracking()
            .Where(l => (l.ClientId == Guid.Empty || l.ClientId == clientId)
                     && l.FieldKey == fieldKey
                     && l.IsActive)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Label)
            .ToListAsync(ct);

        // Count incidents per value for this client
        Dictionary<int, int> incidentCounts = fieldKey switch
        {
            "incident_type" => await _db.Incidents
                .Where(i => i.ClientId == clientId)
                .GroupBy(i => i.Type)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Value, x => x.Count, ct),

            "status" => await _db.Incidents
                .Where(i => i.ClientId == clientId)
                .GroupBy(i => i.Status)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Value, x => x.Count, ct),

            _ => [],
        };

        var result = rows.Select(l => new IncidentLookupDto(
            l.Id, l.ClientId, l.FieldKey, l.Label,
            l.Value, l.SortOrder, l.IsSystem, l.IsActive,
            incidentCounts.TryGetValue(l.Value, out var cnt) ? cnt : 0));

        return Ok(result);
    }

    /// <summary>
    /// Creates a client-specific lookup row. Value is auto-assigned as MAX+1.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<IncidentLookupDto>> CreateLookup(
        [FromBody] CreateLookupRequest req, CancellationToken ct)
    {
        if (req.ClientId == Guid.Empty)
            return BadRequest("clientId must not be the system sentinel value.");

        if (string.IsNullOrWhiteSpace(req.Label))
            return BadRequest("Label is required.");

        if (string.IsNullOrWhiteSpace(req.FieldKey))
            return BadRequest("FieldKey is required.");

        // Auto-assign Value = MAX across system + this client + 1
        var maxValue = await _db.IncidentLookups
            .Where(l => (l.ClientId == Guid.Empty || l.ClientId == req.ClientId)
                     && l.FieldKey == req.FieldKey)
            .Select(l => (int?)l.Value)
            .MaxAsync(ct) ?? 0;

        var nextValue = maxValue + 1;

        var lookup = new IncidentLookup
        {
            Id        = Guid.NewGuid(),
            ClientId  = req.ClientId,
            FieldKey  = req.FieldKey,
            Label     = req.Label.Trim(),
            Value     = nextValue,
            SortOrder = nextValue,
            IsSystem  = false,
            IsActive  = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.IncidentLookups.Add(lookup);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetLookups), new IncidentLookupDto(
            lookup.Id, lookup.ClientId, lookup.FieldKey, lookup.Label,
            lookup.Value, lookup.SortOrder, lookup.IsSystem, lookup.IsActive, Count: 0));
    }

    /// <summary>
    /// Updates label and sortOrder of a client-owned lookup row. Returns 403 for system rows.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLookup(
        Guid id, [FromBody] UpdateLookupRequest req, CancellationToken ct)
    {
        var lookup = await _db.IncidentLookups.FindAsync([id], ct);
        if (lookup is null) return NotFound();

        if (lookup.IsSystem)
            return StatusCode(403, "System rows cannot be edited.");

        if (lookup.ClientId != req.ClientId)
            return StatusCode(403, "ClientId mismatch.");

        if (string.IsNullOrWhiteSpace(req.Label))
            return BadRequest("Label is required.");

        lookup.Label     = req.Label.Trim();
        lookup.SortOrder = req.SortOrder;
        lookup.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes a client-owned lookup row. Returns 403 for system rows.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLookup(
        Guid id, [FromQuery] Guid clientId, CancellationToken ct)
    {
        var lookup = await _db.IncidentLookups.FindAsync([id], ct);
        if (lookup is null) return NotFound();

        if (lookup.IsSystem)
            return StatusCode(403, "System rows cannot be deleted.");

        if (lookup.ClientId != clientId)
            return StatusCode(403, "ClientId mismatch.");

        // Refuse deletion if any incidents reference this value
        var incidentCount = lookup.FieldKey switch
        {
            "incident_type" => await _db.Incidents.CountAsync(
                i => i.ClientId == clientId && i.Type == lookup.Value, ct),
            "status" => await _db.Incidents.CountAsync(
                i => i.ClientId == clientId && i.Status == lookup.Value, ct),
            _ => 0,
        };

        if (incidentCount > 0)
            return Conflict($"Cannot delete: {incidentCount} incident(s) use this value.");

        _db.IncidentLookups.Remove(lookup);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
