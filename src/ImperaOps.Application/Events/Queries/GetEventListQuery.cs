using ImperaOps.Application.Events.Dtos;
using MediatR;

namespace ImperaOps.Application.Events.Queries;

public sealed record GetEventListQuery(
    long ClientId,
    int Page,
    int PageSize,
    long? EventTypeId,
    long? WorkflowStatusId,
    DateTime? DateFrom,
    DateTime? DateTo,
    string? Search
) : IRequest<PagedResult<EventListItemDto>>;
