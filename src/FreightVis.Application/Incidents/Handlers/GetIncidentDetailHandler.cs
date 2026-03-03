using AutoMapper;
using FreightVis.Application.Abstractions;
using FreightVis.Application.Incidents.Dtos;
using FreightVis.Application.Incidents.Queries;
using MediatR;

namespace FreightVis.Application.Incidents.Handlers;

public sealed class GetIncidentDetailHandler : IRequestHandler<GetIncidentDetailQuery, IncidentDetailDto?>
{
    private readonly IIncidentRepository _repo;
    private readonly IMapper _mapper;

    public GetIncidentDetailHandler(IIncidentRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<IncidentDetailDto?> Handle(GetIncidentDetailQuery request, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(request.Id, ct);
        return entity is null ? null : _mapper.Map<IncidentDetailDto>(entity);
    }
}
