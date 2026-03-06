namespace ImperaOps.Infrastructure.Webhooks;

public interface IWebhookDispatcher
{
    Task DispatchAsync(long clientId, string eventType, object payload, CancellationToken ct = default);
}
