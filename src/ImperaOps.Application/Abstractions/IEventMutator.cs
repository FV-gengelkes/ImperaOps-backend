namespace ImperaOps.Application.Abstractions;

/// <summary>
/// Applies mutations to events on behalf of the workflow engine.
/// Each method is a self-contained unit of work (loads, mutates, audits, saves).
/// </summary>
public interface IEventMutator
{
    Task AssignOwnerAsync(long eventId, long clientId, long userId, string ruleName, CancellationToken ct);
    Task ChangeStatusAsync(long eventId, long clientId, long workflowStatusId, string ruleName, CancellationToken ct);
    Task SetRootCauseAsync(long eventId, long clientId, long rootCauseId, string ruleName, CancellationToken ct);
}
