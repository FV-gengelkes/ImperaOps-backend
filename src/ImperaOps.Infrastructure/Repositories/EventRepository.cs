using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Repositories;

public sealed class EventRepository : IEventRepository
{
    private readonly ImperaOpsDbContext _db;

    public EventRepository(ImperaOpsDbContext db) => _db = db;

    public Task<Event?> GetByIdAsync(long id, CancellationToken ct)
        => _db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<long> CreateAsync(Event ev, CancellationToken ct)
    {
        _db.Events.Add(ev);
        await _db.SaveChangesAsync(ct);
        return ev.Id;
    }

    public async Task UpdateAsync(Event ev, CancellationToken ct)
    {
        _db.Events.Update(ev);
        await _db.SaveChangesAsync(ct);
    }

    // ── Tasks ─────────────────────────────────────────────────────────────────

    public Task<EventTask?> GetTaskByIdAsync(long id, CancellationToken ct)
        => _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<int> GetMaxTaskSortOrderAsync(long eventId, CancellationToken ct)
    {
        var max = await _db.Tasks
            .Where(t => t.EventId == eventId)
            .MaxAsync(t => (int?)t.SortOrder, ct);
        return max ?? 0;
    }

    public async Task<long> CreateTaskAsync(EventTask task, CancellationToken ct)
    {
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return task.Id;
    }

    public async Task UpdateTaskAsync(EventTask task, CancellationToken ct)
    {
        _db.Tasks.Update(task);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteTaskAsync(long id, CancellationToken ct)
    {
        var task = await _db.Tasks.FindAsync([id], ct);
        if (task is not null)
        {
            task.DeletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
