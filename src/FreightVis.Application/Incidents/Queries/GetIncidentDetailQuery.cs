using FreightVis.Application.Incidents.Dtos;
using MediatR;

namespace FreightVis.Application.Incidents.Queries;

public sealed record GetIncidentDetailQuery(Guid Id) : IRequest<IncidentDetailDto?>;
