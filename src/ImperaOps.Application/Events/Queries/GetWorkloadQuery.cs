using ImperaOps.Application.Events.Dtos;
using MediatR;

namespace ImperaOps.Application.Events.Queries;

public sealed record GetWorkloadQuery(long ClientId) : IRequest<IReadOnlyList<WorkloadRowDto>>;
