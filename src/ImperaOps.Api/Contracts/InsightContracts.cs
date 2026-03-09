namespace ImperaOps.Api.Contracts;

public sealed record InsightAlertDto(
    long Id,
    long ClientId,
    string AlertType,
    string Severity,
    string Title,
    string Body,
    string? MetadataJson,
    string? RelatedEventIds,
    bool IsAcknowledged,
    string? AcknowledgedAt,
    string GeneratedAt,
    string? AiSummary = null
);

public sealed record InsightSummaryDto(
    int Total,
    int Critical,
    int Warning,
    int Info,
    IReadOnlyList<InsightAlertDto> Recent
);
