using FreightVis.Application.Abstractions;
using FreightVis.Application.Incidents.Dtos;
using FreightVis.Application.Incidents.Queries;
using MediatR;

namespace FreightVis.Application.Incidents.Handlers;

public sealed class GetIncidentListHandler : IRequestHandler<GetIncidentListQuery, PagedResult<IncidentListItemDto>>
{
    private readonly IIncidentReadRepository _readRepo;

    public GetIncidentListHandler(IIncidentReadRepository readRepo) => _readRepo = readRepo;

    public Task<PagedResult<IncidentListItemDto>> Handle(GetIncidentListQuery request, CancellationToken ct)
        => _readRepo.GetListAsync(
            request.ClientId, request.Page, request.PageSize,
            request.Type, request.Status, request.DateFrom, request.DateTo,
            request.Search, ct);
}
