using FreightVis.Domain.Entities;

namespace FreightVis.Application.Abstractions;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(Incident incident, CancellationToken ct);
    Task UpdateAsync(Incident incident, CancellationToken ct);
}
