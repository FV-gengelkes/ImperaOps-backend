using FreightVis.Application.Abstractions;
using FreightVis.Domain.Entities;
using FreightVis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FreightVis.Infrastructure.Repositories;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly FreightVisDbContext _db;

    public IncidentRepository(FreightVisDbContext db) => _db = db;

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Incidents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Guid> CreateAsync(Incident incident, CancellationToken ct)
    {
        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(ct);
        return incident.Id;
    }

    public async Task UpdateAsync(Incident incident, CancellationToken ct)
    {
        _db.Incidents.Update(incident);
        await _db.SaveChangesAsync(ct);
    }
}
