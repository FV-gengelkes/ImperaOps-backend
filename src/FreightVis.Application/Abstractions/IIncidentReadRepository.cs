using FreightVis.Application.Incidents.Dtos;

namespace FreightVis.Application.Abstractions;

public interface IIncidentReadRepository
{
    Task<PagedResult<IncidentListItemDto>> GetListAsync(
        Guid clientId, int page, int pageSize,
        int? type, int? status, DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct);

    Task<IncidentAnalyticsDto> GetAnalyticsAsync(IReadOnlyList<Guid> clientIds, CancellationToken ct);

    Task<IReadOnlyList<IncidentExportRowDto>> GetExportDataAsync(
        Guid clientId, int? type, int? status,
        DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct);
}
