using ImperaOps.Application.Events.Dtos;
using MediatR;

namespace ImperaOps.Application.Events.Queries;

public sealed record GetEventDetailQuery(string PublicId) : IRequest<EventDetailDto?>;
