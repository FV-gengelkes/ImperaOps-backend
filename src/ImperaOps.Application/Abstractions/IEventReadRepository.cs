using ImperaOps.Application.Events.Dtos;

namespace ImperaOps.Application.Abstractions;

public interface IEventReadRepository
{
    Task<PagedResult<EventListItemDto>> GetListAsync(
        long clientId, int page, int pageSize,
        long? eventTypeId, long? workflowStatusId,
        DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct);

    Task<EventDetailDto?> GetByPublicIdAsync(string publicId, CancellationToken ct);

    Task<EventAnalyticsDto> GetAnalyticsAsync(IReadOnlyList<long> clientIds, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct);

    Task<IReadOnlyList<EventExportRowDto>> GetExportDataAsync(
        long clientId, long? eventTypeId, long? workflowStatusId,
        DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct);
}
