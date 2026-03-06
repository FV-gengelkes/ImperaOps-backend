using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Application.Events.Queries;
using MediatR;

namespace ImperaOps.Application.Events.Handlers;

public sealed class GetEventDetailHandler : IRequestHandler<GetEventDetailQuery, EventDetailDto?>
{
    private readonly IEventReadRepository _readRepo;

    public GetEventDetailHandler(IEventReadRepository readRepo) => _readRepo = readRepo;

    public Task<EventDetailDto?> Handle(GetEventDetailQuery request, CancellationToken ct)
        => _readRepo.GetByPublicIdAsync(request.PublicId, ct);
}
