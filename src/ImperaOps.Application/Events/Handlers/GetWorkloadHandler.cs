using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Events.Dtos;
using ImperaOps.Application.Events.Queries;
using MediatR;

namespace ImperaOps.Application.Events.Handlers;

public sealed class GetWorkloadHandler(IEventReadRepository readRepo)
    : IRequestHandler<GetWorkloadQuery, IReadOnlyList<WorkloadRowDto>>
{
    public Task<IReadOnlyList<WorkloadRowDto>> Handle(GetWorkloadQuery request, CancellationToken ct)
        => readRepo.GetWorkloadAsync(request.ClientId, ct);
}
