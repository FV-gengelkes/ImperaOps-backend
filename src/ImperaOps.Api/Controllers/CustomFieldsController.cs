using ImperaOps.Api.Contracts;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/custom-fields")]
public sealed class CustomFieldsController : ControllerBase
{
    private readonly ImperaOpsDbContext _db;

    public CustomFieldsController(ImperaOpsDbContext db) => _db = db;

    // ── Fields ────────────────────────────────────────────────────────────

    /// <summary>Returns all active custom fields for a client, ordered by SortOrder.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> GetFields(
        [FromQuery] long clientId,
        CancellationToken ct)
    {
        if (clientId == 0)
            throw new ValidationException("clientId is required.");

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
        if (req.ClientId == 0)
            throw new ValidationException("clientId is required.");

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Name is required.");

        if (string.IsNullOrWhiteSpace(req.DataType))
            throw new ValidationException("DataType is required.");

        var maxOrder = await _db.CustomFields
            .Where(f => f.ClientId == req.ClientId)
            .Select(f => (int?)f.SortOrder)
            .MaxAsync(ct) ?? 0;

        var field = new CustomField
        {
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
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateField(
        long id,
        [FromBody] UpdateCustomFieldRequest req,
        CancellationToken ct)
    {
        var field = await _db.CustomFields.FindAsync([id], ct);
        if (field is null) throw new NotFoundException();
        if (field.ClientId != req.ClientId) throw new ForbiddenException("ClientId mismatch.");

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("Name is required.");

        if (string.IsNullOrWhiteSpace(req.DataType))
            throw new ValidationException("DataType is required.");

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
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteField(
        long id,
        [FromQuery] long clientId,
        CancellationToken ct)
    {
        var field = await _db.CustomFields.FindAsync([id], ct);
        if (field is null) throw new NotFoundException();
        if (field.ClientId != clientId) throw new ForbiddenException("ClientId mismatch.");

        field.IsActive  = false;
        field.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Values ────────────────────────────────────────────────────────────

    /// <summary>Returns all custom field values for an event, joined with field metadata.</summary>
    [HttpGet("values")]
    public async Task<ActionResult<IReadOnlyList<CustomFieldValueDto>>> GetValues(
        [FromQuery] long entityId,
        [FromQuery] long clientId,
        CancellationToken ct)
    {
        if (entityId == 0)  throw new ValidationException("entityId is required.");
        if (clientId == 0)  throw new ValidationException("clientId is required.");

        var fields = await _db.CustomFields
            .AsNoTracking()
            .Where(f => f.ClientId == clientId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(ct);

        var savedValues = await _db.CustomFieldValues
            .AsNoTracking()
            .Where(v => v.EntityId == entityId)
            .ToListAsync(ct);

        var valDict = savedValues.ToDictionary(v => v.CustomFieldId);

        var result = fields.Select(f =>
        {
            valDict.TryGetValue(f.Id, out var saved);
            return new CustomFieldValueDto(
                saved?.Id ?? 0,
                entityId,
                f.Id,
                f.Name,
                f.DataType,
                f.Options,
                f.IsRequired,
                saved?.Value ?? "");
        }).ToList();

        return Ok(result);
    }

    /// <summary>Upserts all custom field values for an event in one call.</summary>
    [HttpPut("values")]
    public async Task<IActionResult> UpsertValues(
        [FromBody] UpsertCustomFieldValuesRequest req,
        CancellationToken ct)
    {
        if (req.EntityId == 0)  throw new ValidationException("entityId is required.");
        if (req.ClientId == 0)  throw new ValidationException("clientId is required.");

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in req.Values)
        {
            var existing = await _db.CustomFieldValues
                .FirstOrDefaultAsync(
                    v => v.EntityId == req.EntityId && v.CustomFieldId == entry.CustomFieldId,
                    ct);

            if (existing is null)
            {
                _db.CustomFieldValues.Add(new CustomFieldValue
                {
                    EntityId      = req.EntityId,
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
