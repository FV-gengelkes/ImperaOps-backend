using AutoMapper;
using FreightVis.Application.Incidents.Dtos;
using FreightVis.Domain.Entities;

namespace FreightVis.Application.Mapping;

public sealed class IncidentProfile : Profile
{
    public IncidentProfile()
    {
        CreateMap<Incident, IncidentDetailDto>();
    }
}
