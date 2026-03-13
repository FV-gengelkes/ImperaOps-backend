namespace ImperaOps.Application.Abstractions;

/// <summary>
/// Miscellaneous workflow actions: create tasks and add comments.
/// </summary>
public interface IWorkflowActionService
{
    Task CreateTaskAsync(long clientId, long eventId, string title, string? description,
        long? assignedToUserId, int? dueDaysFromNow, string ruleName, CancellationToken ct);

    Task AddCommentAsync(long eventId, long clientId, string body, CancellationToken ct);

    /// <summary>
    /// Picks the next user from the round-robin pool for the given rule and returns their ID.
    /// </summary>
    Task<long> GetNextRoundRobinUserAsync(long workflowRuleId, long[] userIds, CancellationToken ct);
}
