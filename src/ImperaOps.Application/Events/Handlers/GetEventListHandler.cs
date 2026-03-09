using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Application.Events.Queries;
using MediatR;

namespace ImperaOps.Application.Events.Handlers;

public sealed class GetEventListHandler : IRequestHandler<GetEventListQuery, PagedResult<EventListItemDto>>
{
    private readonly IEventReadRepository _readRepo;

    public GetEventListHandler(IEventReadRepository readRepo) => _readRepo = readRepo;

    public Task<PagedResult<EventListItemDto>> Handle(GetEventListQuery request, CancellationToken ct)
        => _readRepo.GetListAsync(
            request.ClientId, request.Page, request.PageSize,
            request.EventTypeId, request.WorkflowStatusId,
            request.DateFrom, request.DateTo, request.Search, ct,
            request.SlaBreached, request.IsClosed);
}
