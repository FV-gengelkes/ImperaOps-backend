using ImperaOps.Domain.Entities;

namespace ImperaOps.Application.Abstractions;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(long id, CancellationToken ct);
    Task<long> CreateAsync(Event ev, CancellationToken ct);
    Task UpdateAsync(Event ev, CancellationToken ct);

    // Tasks
    Task<EventTask?> GetTaskByIdAsync(long id, CancellationToken ct);
    Task<int> GetMaxTaskSortOrderAsync(long eventId, CancellationToken ct);
    Task<long> CreateTaskAsync(EventTask task, CancellationToken ct);
    Task UpdateTaskAsync(EventTask task, CancellationToken ct);
    Task DeleteTaskAsync(long id, CancellationToken ct);
}
