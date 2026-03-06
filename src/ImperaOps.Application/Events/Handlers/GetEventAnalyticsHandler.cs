using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Application.Events.Queries;
using MediatR;

namespace ImperaOps.Application.Events.Handlers;

public sealed class GetEventAnalyticsHandler : IRequestHandler<GetEventAnalyticsQuery, EventAnalyticsDto>
{
    private readonly IEventReadRepository _readRepo;

    public GetEventAnalyticsHandler(IEventReadRepository readRepo) => _readRepo = readRepo;

    public Task<EventAnalyticsDto> Handle(GetEventAnalyticsQuery request, CancellationToken ct)
        => _readRepo.GetAnalyticsAsync(request.ClientIds, request.DateFrom, request.DateTo, ct);
}
