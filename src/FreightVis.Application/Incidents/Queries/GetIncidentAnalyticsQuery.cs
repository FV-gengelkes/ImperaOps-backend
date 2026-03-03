using FreightVis.Application.Incidents.Dtos;
using MediatR;

namespace FreightVis.Application.Incidents.Queries;

public sealed record GetIncidentAnalyticsQuery(IReadOnlyList<Guid> ClientIds) : IRequest<IncidentAnalyticsDto>;
