using ImperaOps.Application.Events.Dtos;

namespace ImperaOps.Application.Abstractions;

public interface IEventReadRepository
{
    Task<PagedResult<EventListItemDto>> GetListAsync(
        long clientId, int page, int pageSize,
        long? eventTypeId, long? workflowStatusId,
        DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct,
        bool slaBreached = false,
        bool? isClosed = null);

    Task<EventDetailDto?> GetByPublicIdAsync(string publicId, CancellationToken ct, long? clientId = null);

    Task<EventAnalyticsDto> GetAnalyticsAsync(IReadOnlyList<long> clientIds, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct);

    Task<IReadOnlyList<EventExportRowDto>> GetExportDataAsync(
        long clientId, long? eventTypeId, long? workflowStatusId,
        DateTime? dateFrom, DateTime? dateTo,
        string? search, CancellationToken ct);

    Task<IReadOnlyList<WorkloadRowDto>> GetWorkloadAsync(long clientId, CancellationToken ct);

    Task<SlaRuleMatch?> GetSlaRuleForEventAsync(long clientId, long eventTypeId, CancellationToken ct);
}

public sealed class SlaRuleMatch
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public int? InvestigationHours { get; set; }
    public int? ClosureHours { get; set; }
}
