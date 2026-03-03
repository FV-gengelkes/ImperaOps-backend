using FreightVis.Application.Incidents.Dtos;
using MediatR;

namespace FreightVis.Application.Incidents.Queries;

public sealed record GetIncidentListQuery(
    Guid ClientId,
    int Page,
    int PageSize,
    int? Type = null,
    int? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Search = null
) : IRequest<PagedResult<IncidentListItemDto>>;
