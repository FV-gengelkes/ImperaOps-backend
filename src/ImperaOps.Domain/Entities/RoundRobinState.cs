namespace ImperaOps.Domain.Entities;

/// <summary>
/// Tracks the last-assigned index for round-robin workflow rule actions.
/// One row per workflow rule that uses round_robin_assign.
/// </summary>
public sealed class RoundRobinState
{
    public long WorkflowRuleId { get; set; }
    public int LastAssignedIndex { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
