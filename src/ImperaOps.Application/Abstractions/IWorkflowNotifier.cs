namespace ImperaOps.Application.Abstractions;

/// <summary>
/// Sends notifications on behalf of the workflow engine.
/// </summary>
public interface IWorkflowNotifier
{
    Task NotifyEventAssignedAsync(long userId, long clientId, string eventPublicId, string eventTitle, CancellationToken ct);

    Task NotifyUsersAsync(
        long clientId, string eventPublicId, string ruleName, string message,
        long[]? userIds, string[]? roles, CancellationToken ct);
}
