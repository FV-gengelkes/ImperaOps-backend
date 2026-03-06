namespace ImperaOps.Application.Events.Dtos;

// ── Dapper-materialized DTOs: use class + public setters for reliable nullable mapping ──

public sealed class EventListItemDto
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string PublicId { get; set; } = "";
    public long EventTypeId { get; set; }
    public string EventTypeName { get; set; } = "";
    public long WorkflowStatusId { get; set; }
    public string WorkflowStatusName { get; set; } = "";
    public string? WorkflowStatusColor { get; set; }
    public bool WorkflowStatusIsClosed { get; set; }
    public string Title { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string Location { get; set; } = "";
    public long? OwnerUserId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public long ReferenceNumber { get; set; }
}

public sealed class EventDetailDto
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string PublicId { get; set; } = "";
    public long EventTypeId { get; set; }
    public string EventTypeName { get; set; } = "";
    public long WorkflowStatusId { get; set; }
    public string WorkflowStatusName { get; set; } = "";
    public string? WorkflowStatusColor { get; set; }
    public bool WorkflowStatusIsClosed { get; set; }
    public string Title { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public long? ReportedByUserId { get; set; }
    public string? ReportedByDisplayName { get; set; }
    public string? ExternalReporterName { get; set; }
    public string? ExternalReporterContact { get; set; }
    public long? OwnerUserId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public long ReferenceNumber { get; set; }
    public long? RootCauseId { get; set; }
    public string? RootCauseName { get; set; }
    public string? CorrectiveAction { get; set; }
    public SlaStatusDto? Sla { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record EventAnalyticsDto(
    int Total,
    int Open,
    int InProgress,
    int Blocked,
    int Closed,
    int ThisMonth,
    int LastMonth,
    IReadOnlyList<EventTypeCountDto> ByType,
    IReadOnlyList<EventMonthlyRowDto> ByMonth,
    IReadOnlyList<EventLocationCountDto> TopLocations,
    IReadOnlyList<EventLocationTypeCountDto> ByLocationAndType,
    IReadOnlyList<EventRootCauseCountDto> ByRootCause,
    double? AvgResolutionDays,
    double? SlaClosureComplianceRate
);

public sealed record EventRootCauseCountDto(string Name, long Count);

public sealed record EventTypeCountDto(long EventTypeId, string EventTypeName, long Count);
public sealed record EventMonthlyRowDto(int Year, int Month, long EventTypeId, string EventTypeName, long Count);
public sealed record EventLocationCountDto(string Location, long Count);
public sealed record EventLocationTypeCountDto(string Location, long EventTypeId, string EventTypeName, long Count);

public sealed class SlaStatusDto
{
    public long RuleId { get; set; }
    public string RuleName { get; set; } = "";
    public DateTimeOffset? InvestigationDeadline { get; set; }
    public bool InvestigationBreached { get; set; }
    public DateTimeOffset? ClosureDeadline { get; set; }
    public bool ClosureBreached { get; set; }
}

public sealed class EventExportRowDto
{
    public string PublicId { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string EventTypeName { get; set; } = "";
    public string WorkflowStatusName { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Owner { get; set; }
}
