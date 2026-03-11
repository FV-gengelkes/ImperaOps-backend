using ImperaOps.Domain.Entities;

namespace ImperaOps.Application.Abstractions;

public interface IWorkflowRuleRepository
{
    Task<IReadOnlyList<WorkflowRule>> GetActiveRulesAsync(long clientId, string triggerType, CancellationToken ct);
    Task SaveExecutionAsync(WorkflowRuleExecution execution, CancellationToken ct);
}
