namespace ImperaOps.Infrastructure.Notifications;

public interface INotificationPushService
{
    IAsyncEnumerable<string> SubscribeAsync(long userId, CancellationToken ct);
    void Push(long userId, string message);
}
