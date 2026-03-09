namespace ImperaOps.Infrastructure.Ai;

public interface IClaudeService
{
    Task<AiCategorizationResult> CategorizeAsync(
        string title,
        string description,
        IReadOnlyList<NamedItem> eventTypes,
        IReadOnlyList<NamedItem> rootCauses,
        CancellationToken ct = default);

    Task<AiInvestigationResult> SuggestInvestigationAsync(
        string eventTitle,
        string description,
        string? location,
        string? summary,
        IReadOnlyList<string> witnessStatements,
        IReadOnlyList<string> evidenceDescriptions,
        CancellationToken ct = default);

    Task<string> AnalyzeTrendsAsync(
        IReadOnlyList<AlertInfo> recentAlerts,
        CancellationToken ct = default);
}

public sealed record NamedItem(long Id, string Name);

public sealed record AiCategorizationResult(
    long? SuggestedEventTypeId,
    string? SuggestedEventTypeName,
    double EventTypeConfidence,
    long? SuggestedRootCauseId,
    string? SuggestedRootCauseName,
    double RootCauseConfidence,
    string Reasoning);

public sealed record AiInvestigationResult(
    string? SuggestedRootCause,
    string? SuggestedCorrectiveActions,
    string Reasoning);

public sealed record AlertInfo(
    string AlertType,
    string Severity,
    string Title,
    string Body);
