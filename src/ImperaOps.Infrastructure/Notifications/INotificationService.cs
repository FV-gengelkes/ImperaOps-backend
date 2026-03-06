namespace ImperaOps.Infrastructure.Notifications;

public interface INotificationService
{
    Task NotifyEventAssignedAsync(long newOwnerUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string eventTitle, CancellationToken ct = default);
    Task NotifyTaskAssignedAsync(long assignedToUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string taskPublicId, string taskTitle, CancellationToken ct = default);
    Task ClearTaskNotificationAsync(string taskPublicId, CancellationToken ct = default);
    Task NotifyCommentAddedAsync(long eventOwnerUserId, long reportedByUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string eventTitle, string commentSnippet, IReadOnlyList<long>? mentionedUserIds = null, CancellationToken ct = default);
    Task NotifyStatusChangedAsync(long eventOwnerUserId, long actorUserId, string actorName, long clientId, string eventPublicId, string eventTitle, string newStatusName, CancellationToken ct = default);
}
