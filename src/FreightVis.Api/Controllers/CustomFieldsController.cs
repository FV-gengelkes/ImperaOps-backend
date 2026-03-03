using FreightVis.Api.Contracts;
using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreightVis.Api.Controllers;

[ApiController]
[Route("api/v1/custom-fields")]
public sealed class CustomFieldsController : ControllerBase
{
    private readonly FreightVisDbContext _db;

    public CustomFieldsController(FreightVisDbContext db) => _db = db;

    // ── Fields ────────────────────────────────────────────────────────────

    /// <summary>Returns all active custom fields for a client, ordered by SortOrder.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> GetFields(
        [FromQuery] Guid clientId,
        CancellationToken ct)
    {
        if (clientId == Guid.Empty)
            return BadRequest("clientId is required.");

        var fields = await _db.CustomFields
            .AsNoTracking()
            .Where(f => f.ClientId == clientId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Select(f => new CustomFieldDto(
                f.Id, f.ClientId, f.Name, f.DataType,
                f.IsRequired, f.SortOrder, f.IsActive, f.Options))
            .ToListAsync(ct);

        return Ok(fields);
    }

    /// <summary>Creates a new custom field for the given client.</summary>
    [HttpPost]
    public async Task<ActionResult<CustomFieldDto>> CreateField(
        [FromBody] CreateCustomFieldRequest req,
        CancellationToken ct)
    {
        if (req.ClientId == Guid.Empty)
            return BadRequest("clientId is required.");

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        if (string.IsNullOrWhiteSpace(req.DataType))
            return BadRequest("DataType is required.");

        var maxOrder = await _db.CustomFields
            .Where(f => f.ClientId == req.ClientId)
            .Select(f => (int?)f.SortOrder)
            .MaxAsync(ct) ?? 0;

        var field = new CustomField
        {
            Id         = Guid.NewGuid(),
            ClientId   = req.ClientId,
            Name       = req.Name.Trim(),
            DataType   = req.DataType,
            IsRequired = req.IsRequired,
            SortOrder  = maxOrder + 1,
            IsActive   = true,
            Options    = req.Options,
            CreatedAt  = DateTimeOffset.UtcNow,
            UpdatedAt  = DateTimeOffset.UtcNow,
        };

        _db.CustomFields.Add(field);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetFields),
            new CustomFieldDto(field.Id, field.ClientId, field.Name, field.DataType,
                field.IsRequired, field.SortOrder, field.IsActive, field.Options));
    }

    /// <summary>Updates an existing custom field. Returns 403 if clientId doesn't match.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateField(
        Guid id,
        [FromBody] UpdateCustomFieldRequest req,
        CancellationToken ct)
    {
        var field = await _db.CustomFields.FindAsync([id], ct);
        if (field is null) return NotFound();
        if (field.ClientId != req.ClientId) return StatusCode(403, "ClientId mismatch.");

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        if (string.IsNullOrWhiteSpace(req.DataType))
            return BadRequest("DataType is required.");

        field.Name       = req.Name.Trim();
        field.DataType   = req.DataType;
        field.IsRequired = req.IsRequired;
        field.SortOrder  = req.SortOrder;
        field.Options    = req.Options;
        field.UpdatedAt  = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Soft-deletes a custom field (sets IsActive = false).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteField(
        Guid id,
        [FromQuery] Guid clientId,
        CancellationToken ct)
    {
        var field = await _db.CustomFields.FindAsync([id], ct);
        if (field is null) return NotFound();
        if (field.ClientId != clientId) return StatusCode(403, "ClientId mismatch.");

        field.IsActive  = false;
        field.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Values ────────────────────────────────────────────────────────────

    /// <summary>Returns all custom field values for an incident, joined with field metadata.</summary>
    [HttpGet("values")]
    public async Task<ActionResult<IReadOnlyList<CustomFieldValueDto>>> GetValues(
        [FromQuery] Guid incidentId,
        [FromQuery] Guid clientId,
        CancellationToken ct)
    {
        if (incidentId == Guid.Empty) return BadRequest("incidentId is required.");
        if (clientId == Guid.Empty)  return BadRequest("clientId is required.");

        // Load all active fields for the client
        var fields = await _db.CustomFields
            .AsNoTracking()
            .Where(f => f.ClientId == clientId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(ct);

        // Load existing saved values for this incident
        var savedValues = await _db.CustomFieldValues
            .AsNoTracking()
            .Where(v => v.IncidentId == incidentId)
            .ToListAsync(ct);

        var valDict = savedValues.ToDictionary(v => v.CustomFieldId);

        var result = fields.Select(f =>
        {
            valDict.TryGetValue(f.Id, out var saved);
            return new CustomFieldValueDto(
                saved?.Id ?? Guid.Empty,
                incidentId,
                f.Id,
                f.Name,
                f.DataType,
                f.Options,
                f.IsRequired,
                saved?.Value ?? "");
        }).ToList();

        return Ok(result);
    }

    /// <summary>Upserts all custom field values for an incident in one call.</summary>
    [HttpPut("values")]
    public async Task<IActionResult> UpsertValues(
        [FromBody] UpsertCustomFieldValuesRequest req,
        CancellationToken ct)
    {
        if (req.IncidentId == Guid.Empty) return BadRequest("incidentId is required.");
        if (req.ClientId == Guid.Empty)   return BadRequest("clientId is required.");

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in req.Values)
        {
            var existing = await _db.CustomFieldValues
                .FirstOrDefaultAsync(
                    v => v.IncidentId == req.IncidentId && v.CustomFieldId == entry.CustomFieldId,
                    ct);

            if (existing is null)
            {
                _db.CustomFieldValues.Add(new CustomFieldValue
                {
                    Id            = Guid.NewGuid(),
                    IncidentId    = req.IncidentId,
                    CustomFieldId = entry.CustomFieldId,
                    Value         = entry.Value,
                    CreatedAt     = now,
                    UpdatedAt     = now,
                });
            }
            else
            {
                existing.Value     = entry.Value;
                existing.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
