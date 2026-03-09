namespace ImperaOps.Api.Contracts;

public sealed record AiCategorizeRequest(
    string Title,
    string Description,
    long ClientId
);

public sealed record AiCategorizeResponse(
    long? SuggestedEventTypeId,
    string? SuggestedEventTypeName,
    double EventTypeConfidence,
    long? SuggestedRootCauseId,
    string? SuggestedRootCauseName,
    double RootCauseConfidence,
    string Reasoning
);

public sealed record AiInvestigateRequest(
    string PublicId
);

public sealed record AiInvestigateResponse(
    string? SuggestedRootCause,
    string? SuggestedCorrectiveActions,
    string Reasoning
);

public sealed record AiTrendAnalysisRequest(
    long ClientId
);

public sealed record AiTrendAnalysisResponse(
    string Summary
);
