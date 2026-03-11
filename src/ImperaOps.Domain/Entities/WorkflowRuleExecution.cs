namespace ImperaOps.Domain.Entities;

public sealed class WorkflowRuleExecution
{
    public long Id { get; set; }
    public long WorkflowRuleId { get; set; }
    public long ClientId { get; set; }
    public long EventId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public int ActionsExecuted { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
}
