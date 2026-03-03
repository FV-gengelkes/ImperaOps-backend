using FreightVis.Application.Abstractions;
using FreightVis.Application.Incidents.Dtos;
using FreightVis.Application.Incidents.Queries;
using MediatR;

namespace FreightVis.Application.Incidents.Handlers;

public sealed class GetIncidentAnalyticsHandler : IRequestHandler<GetIncidentAnalyticsQuery, IncidentAnalyticsDto>
{
    private readonly IIncidentReadRepository _readRepo;

    public GetIncidentAnalyticsHandler(IIncidentReadRepository readRepo) => _readRepo = readRepo;

    public Task<IncidentAnalyticsDto> Handle(GetIncidentAnalyticsQuery request, CancellationToken ct)
        => _readRepo.GetAnalyticsAsync(request.ClientIds, ct);
}
