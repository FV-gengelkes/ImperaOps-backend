using FreightVis.Application.Abstractions;
using FreightVis.Application.Incidents.Commands;
using FreightVis.Domain.Entities;
using MediatR;

namespace FreightVis.Application.Incidents.Handlers;

public sealed class CreateIncidentHandler : IRequestHandler<CreateIncidentCommand, CreateIncidentResult>
{
    private readonly IIncidentRepository _repo;

    public CreateIncidentHandler(IIncidentRepository repo) => _repo = repo;

    public async Task<CreateIncidentResult> Handle(CreateIncidentCommand request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            Type = request.Type,
            Status = 1, // Open
            OccurredAt = request.OccurredAt,
            Location = request.Location,
            Description = request.Description,
            ReportedByUserId = request.ReportedByUserId,
            OwnerUserId = null,
            ReferenceNumber = request.ReferenceNumber,
            CreatedAt = now,
            UpdatedAt = now
        };

        var id = await _repo.CreateAsync(incident, ct);
        return new CreateIncidentResult(id);
    }
}
