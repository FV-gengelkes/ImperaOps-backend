using ImperaOps.Api.Contracts;
using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Domain.Exceptions;
using ImperaOps.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("api/v1/clients/{clientId:long}/ag")]
[Authorize]
public sealed class AgFieldMappingController(
    ImperaOpsDbContext db,
    ICounterService counters) : ScopedControllerBase
{
    private const string ModuleId = "ag_field_mapping";

    // ── Fields ────────────────────────────────────────────────────────────

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<AgFieldListItemDto>>> GetFields(
        long clientId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);

        var jobCounts = await db.SprayJobs
            .AsNoTracking()
            .Where(j => j.ClientId == clientId)
            .GroupBy(j => j.FieldId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var fields = await db.AgFields
            .AsNoTracking()
            .Where(f => f.ClientId == clientId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        return Ok(fields.Select(f => new AgFieldListItemDto(
            f.Id, f.Name, f.Acreage, f.GrowerName, f.Address,
            jobCounts.TryGetValue(f.Id, out var c) ? c : 0,
            f.CreatedAt)).ToList());
    }

    [HttpGet("fields/{fieldId:long}")]
    public async Task<ActionResult<AgFieldDto>> GetField(
        long clientId, long fieldId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);

        var f = await db.AgFields.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == fieldId && x.ClientId == clientId, ct);
        if (f is null) throw new NotFoundException();

        var jobCount = await db.SprayJobs.CountAsync(j => j.FieldId == fieldId && j.ClientId == clientId, ct);

        return Ok(new AgFieldDto(
            f.Id, f.ClientId, f.Name, f.Acreage,
            f.GrowerName, f.GrowerContact, f.Address,
            f.BoundaryGeoJson, f.Notes, jobCount,
            f.CreatedAt, f.UpdatedAt));
    }

    [HttpPost("fields")]
    public async Task<ActionResult<AgFieldDto>> CreateField(
        long clientId, [FromBody] CreateAgFieldRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);
        if (!await IsManagerOrAboveAsync(db, clientId, User, ct)) throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");

        var now = DateTimeOffset.UtcNow;
        var field = new AgField
        {
            ClientId        = clientId,
            Name            = req.Name.Trim(),
            Acreage         = req.Acreage,
            GrowerName      = req.GrowerName?.Trim(),
            GrowerContact   = req.GrowerContact?.Trim(),
            Address         = req.Address?.Trim(),
            BoundaryGeoJson = req.BoundaryGeoJson,
            Notes           = req.Notes?.Trim(),
            CreatedAt       = now,
            UpdatedAt       = now,
        };

        db.AgFields.Add(field);
        await db.SaveChangesAsync(ct);

        Audit.Record("ag_field", field.Id, clientId, "created",
            $"Field \"{field.Name}\" created.");
        await db.SaveChangesAsync(ct);

        return Ok(new AgFieldDto(
            field.Id, field.ClientId, field.Name, field.Acreage,
            field.GrowerName, field.GrowerContact, field.Address,
            field.BoundaryGeoJson, field.Notes, 0,
            field.CreatedAt, field.UpdatedAt));
    }

    [HttpPut("fields/{fieldId:long}")]
    public async Task<IActionResult> UpdateField(
        long clientId, long fieldId, [FromBody] UpdateAgFieldRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);
        if (!await IsManagerOrAboveAsync(db, clientId, User, ct)) throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ValidationException("Name is required.");

        var field = await db.AgFields.FirstOrDefaultAsync(x => x.Id == fieldId && x.ClientId == clientId, ct);
        if (field is null) throw new NotFoundException();

        field.Name            = req.Name.Trim();
        field.Acreage         = req.Acreage;
        field.GrowerName      = req.GrowerName?.Trim();
        field.GrowerContact   = req.GrowerContact?.Trim();
        field.Address         = req.Address?.Trim();
        field.BoundaryGeoJson = req.BoundaryGeoJson;
        field.Notes           = req.Notes?.Trim();
        field.UpdatedAt       = DateTimeOffset.UtcNow;

        Audit.Record("ag_field", field.Id, clientId, "updated",
            $"Field \"{field.Name}\" updated.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("fields/{fieldId:long}")]
    public async Task<IActionResult> DeleteField(
        long clientId, long fieldId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);
        if (!await IsManagerOrAboveAsync(db, clientId, User, ct)) throw new ForbiddenException();

        var field = await db.AgFields.FirstOrDefaultAsync(x => x.Id == fieldId && x.ClientId == clientId, ct);
        if (field is null) throw new NotFoundException();

        field.DeletedAt = DateTimeOffset.UtcNow;

        Audit.Record("ag_field", field.Id, clientId, "deleted",
            $"Field \"{field.Name}\" deleted.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Spray Jobs ────────────────────────────────────────────────────────

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<SprayJobListItemDto>>> GetJobs(
        long clientId, [FromQuery] long? fieldId, [FromQuery] string? status, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);

        var fieldNames = await db.AgFields
            .AsNoTracking()
            .Where(f => f.ClientId == clientId)
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        var query = db.SprayJobs
            .AsNoTracking()
            .Where(j => j.ClientId == clientId);

        if (fieldId.HasValue)
            query = query.Where(j => j.FieldId == fieldId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(j => j.Status == status);

        var jobs = await query
            .OrderByDescending(j => j.ScheduledDate ?? j.CreatedAt)
            .ToListAsync(ct);

        return Ok(jobs.Select(j => new SprayJobListItemDto(
            j.Id, j.JobNumber, j.FieldId,
            fieldNames.TryGetValue(j.FieldId, out var fn) ? fn : null,
            j.Status, j.ScheduledDate, j.CompletedDate,
            j.DroneOperator, j.Product, j.CreatedAt)).ToList());
    }

    [HttpGet("jobs/{jobId:long}")]
    public async Task<ActionResult<SprayJobDto>> GetJob(
        long clientId, long jobId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);

        var j = await db.SprayJobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId && x.ClientId == clientId, ct);
        if (j is null) throw new NotFoundException();

        var fieldName = await db.AgFields
            .AsNoTracking()
            .Where(f => f.Id == j.FieldId && f.ClientId == clientId)
            .Select(f => f.Name)
            .FirstOrDefaultAsync(ct);

        return Ok(new SprayJobDto(
            j.Id, j.ClientId, j.FieldId, fieldName,
            j.JobNumber, j.Status, j.ScheduledDate, j.CompletedDate,
            j.DroneOperator, j.Product, j.ApplicationRate, j.ApplicationUnit,
            j.WeatherConditions, j.FlightLogGeoJson, j.Notes,
            j.CreatedAt, j.UpdatedAt));
    }

    [HttpPost("jobs")]
    public async Task<ActionResult<SprayJobDto>> CreateJob(
        long clientId, [FromBody] CreateSprayJobRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);
        if (!await IsManagerOrAboveAsync(db, clientId, User, ct)) throw new ForbiddenException();

        var field = await db.AgFields.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == req.FieldId && f.ClientId == clientId, ct);
        if (field is null) throw new ValidationException("Field not found.");

        var refNum = await counters.AllocateAsync(clientId, "spray_job", ct);
        var now = DateTimeOffset.UtcNow;

        var job = new SprayJob
        {
            ClientId        = clientId,
            FieldId         = req.FieldId,
            JobNumber       = $"JOB-{refNum:D4}",
            ReferenceNumber = refNum,
            Status          = "scheduled",
            ScheduledDate   = req.ScheduledDate,
            DroneOperator   = req.DroneOperator?.Trim(),
            Product         = req.Product?.Trim(),
            ApplicationRate = req.ApplicationRate?.Trim(),
            ApplicationUnit = req.ApplicationUnit?.Trim(),
            WeatherConditions = req.WeatherConditions?.Trim(),
            Notes           = req.Notes?.Trim(),
            CreatedAt       = now,
            UpdatedAt       = now,
        };

        db.SprayJobs.Add(job);
        await db.SaveChangesAsync(ct);

        Audit.Record("spray_job", job.Id, clientId, "created",
            $"Spray job {job.JobNumber} created for field \"{field.Name}\".");
        await db.SaveChangesAsync(ct);

        return Ok(new SprayJobDto(
            job.Id, job.ClientId, job.FieldId, field.Name,
            job.JobNumber, job.Status, job.ScheduledDate, job.CompletedDate,
            job.DroneOperator, job.Product, job.ApplicationRate, job.ApplicationUnit,
            job.WeatherConditions, job.FlightLogGeoJson, job.Notes,
            job.CreatedAt, job.UpdatedAt));
    }

    [HttpPut("jobs/{jobId:long}")]
    public async Task<IActionResult> UpdateJob(
        long clientId, long jobId, [FromBody] UpdateSprayJobRequest req, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);
        if (!await IsInvestigatorOrAboveAsync(db, clientId, User, ct)) throw new ForbiddenException();

        var allowed = new[] { "scheduled", "in_progress", "completed", "cancelled" };
        if (!allowed.Contains(req.Status))
            throw new ValidationException($"Invalid status. Allowed: {string.Join(", ", allowed)}.");

        var job = await db.SprayJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.ClientId == clientId, ct);
        if (job is null) throw new NotFoundException();

        var field = await db.AgFields.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == req.FieldId && f.ClientId == clientId, ct);
        if (field is null) throw new ValidationException("Field not found.");

        var oldStatus = job.Status;
        job.FieldId           = req.FieldId;
        job.Status            = req.Status;
        job.ScheduledDate     = req.ScheduledDate;
        job.CompletedDate     = req.CompletedDate;
        job.DroneOperator     = req.DroneOperator?.Trim();
        job.Product           = req.Product?.Trim();
        job.ApplicationRate   = req.ApplicationRate?.Trim();
        job.ApplicationUnit   = req.ApplicationUnit?.Trim();
        job.WeatherConditions = req.WeatherConditions?.Trim();
        job.FlightLogGeoJson  = req.FlightLogGeoJson;
        job.Notes             = req.Notes?.Trim();
        job.UpdatedAt         = DateTimeOffset.UtcNow;

        var detail = oldStatus != req.Status
            ? $"Spray job {job.JobNumber} updated (status: {oldStatus} → {req.Status})."
            : $"Spray job {job.JobNumber} updated.";
        Audit.Record("spray_job", job.Id, clientId, "updated", detail);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("jobs/{jobId:long}")]
    public async Task<IActionResult> DeleteJob(
        long clientId, long jobId, CancellationToken ct)
    {
        RequireClientAccess(clientId);
        await RequireModuleAsync(db, clientId, ModuleId, ct);
        if (!await IsManagerOrAboveAsync(db, clientId, User, ct)) throw new ForbiddenException();

        var job = await db.SprayJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.ClientId == clientId, ct);
        if (job is null) throw new NotFoundException();

        job.DeletedAt = DateTimeOffset.UtcNow;

        Audit.Record("spray_job", job.Id, clientId, "deleted",
            $"Spray job {job.JobNumber} deleted.");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
