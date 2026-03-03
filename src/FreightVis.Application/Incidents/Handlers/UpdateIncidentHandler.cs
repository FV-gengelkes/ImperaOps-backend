using FreightVis.Application.Abstractions;
using FreightVis.Application.Incidents.Commands;
using MediatR;

namespace FreightVis.Application.Incidents.Handlers;

public sealed class UpdateIncidentHandler : IRequestHandler<UpdateIncidentCommand, Unit>
{
    private readonly IIncidentRepository _repo;

    public UpdateIncidentHandler(IIncidentRepository repo) => _repo = repo;

    public async Task<Unit> Handle(UpdateIncidentCommand request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(request.Id, ct);
        if (existing is null) return Unit.Value;

        existing.Type = request.Type;
        existing.Status = request.Status;
        existing.OccurredAt = request.OccurredAt;
        existing.Location = request.Location;
        existing.Description = request.Description;
        existing.OwnerUserId = request.OwnerUserId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _repo.UpdateAsync(existing, ct);
        return Unit.Value;
    }
}
