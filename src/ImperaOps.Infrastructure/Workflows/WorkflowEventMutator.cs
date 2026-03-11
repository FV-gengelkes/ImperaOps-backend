using ImperaOps.Application.Abstractions;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Workflows;

public sealed class WorkflowEventMutator : IEventMutator
{
    private readonly ImperaOpsDbContext _db;
    private readonly IAuditService _audit;

    public WorkflowEventMutator(ImperaOpsDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task AssignOwnerAsync(long eventId, long clientId, long userId, string ruleName, CancellationToken ct)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (entity == null) return;

        entity.OwnerUserId = userId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        _audit.Record("event", eventId, clientId, "owner_changed",
            $"Auto-assigned by workflow rule \"{ruleName}\".");
        await _db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(long eventId, long clientId, long workflowStatusId, string ruleName, CancellationToken ct)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (entity == null) return;

        var oldStatusName = await _db.WorkflowStatuses.AsNoTracking()
            .Where(s => s.Id == entity.WorkflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        var newStatusName = await _db.WorkflowStatuses.AsNoTracking()
            .Where(s => s.Id == workflowStatusId).Select(s => s.Name).FirstOrDefaultAsync(ct);

        entity.WorkflowStatusId = workflowStatusId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        _audit.Record("event", eventId, clientId, "status_changed",
            $"Status changed from \"{oldStatusName}\" to \"{newStatusName}\" by workflow rule \"{ruleName}\".");
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetRootCauseAsync(long eventId, long clientId, long rootCauseId, string ruleName, CancellationToken ct)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (entity == null) return;

        entity.RootCauseId = rootCauseId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var rcName = await _db.RootCauseTaxonomyItems.AsNoTracking()
            .Where(r => r.Id == rootCauseId).Select(r => r.Name).FirstOrDefaultAsync(ct);

        _audit.Record("event", eventId, clientId, "root_cause_set",
            $"Root cause set to \"{rcName}\" by workflow rule \"{ruleName}\".");
        await _db.SaveChangesAsync(ct);
    }
}
