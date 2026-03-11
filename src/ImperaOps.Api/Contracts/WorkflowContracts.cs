using System.Text.Json;

namespace ImperaOps.Api.Contracts;

public sealed record WorkflowRuleDto(
    long Id,
    long ClientId,
    string Name,
    string? Description,
    string TriggerType,
    bool IsActive,
    int SortOrder,
    bool StopOnMatch,
    JsonElement Conditions,
    JsonElement Actions,
    long? CreatedByUserId,
    string? CreatedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int ExecutionCount,
    int FailedExecutionCount
);

public sealed record CreateWorkflowRuleRequest(
    string Name,
    string? Description,
    string TriggerType,
    bool IsActive,
    bool StopOnMatch,
    JsonElement Conditions,
    JsonElement Actions
);

public sealed record UpdateWorkflowRuleRequest(
    string Name,
    string? Description,
    string TriggerType,
    bool IsActive,
    bool StopOnMatch,
    JsonElement Conditions,
    JsonElement Actions
);

public sealed record WorkflowRuleExecutionDto(
    long Id,
    long WorkflowRuleId,
    string? WorkflowRuleName,
    long EventId,
    string? EventPublicId,
    string TriggerType,
    int ActionsExecuted,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset ExecutedAt
);

public sealed record ReorderWorkflowRulesRequest(
    long[] OrderedIds
);
