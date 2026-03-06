using ImperaOps.Application.Events.Dtos;
using MediatR;

namespace ImperaOps.Application.Events.Queries;

public sealed record GetEventAnalyticsQuery(IReadOnlyList<long> ClientIds, DateTime? DateFrom, DateTime? DateTo) : IRequest<EventAnalyticsDto>;
