using System.Text.Json;
using Hangfire;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Webhooks;

public class WebhookDispatcher : IWebhookDispatcher
{
    private readonly ImperaOpsDbContext _db;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<WebhookDispatcher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public WebhookDispatcher(ImperaOpsDbContext db, IBackgroundJobClient jobs, ILogger<WebhookDispatcher> logger)
    {
        _db     = db;
        _jobs   = jobs;
        _logger = logger;
    }

    public async Task DispatchAsync(long clientId, string eventType, object payload, CancellationToken ct = default)
    {
        try
        {
            var webhooks = await _db.ClientWebhooks
                .AsNoTracking()
                .Where(w => w.ClientId == clientId && w.IsActive)
                .ToListAsync(ct);

            if (webhooks.Count == 0) return;

            var jsonPayload = JsonSerializer.Serialize(payload, JsonOptions);

            foreach (var webhook in webhooks)
            {
                var subscribedTypes = JsonSerializer.Deserialize<string[]>(webhook.EventTypes) ?? [];
                if (!subscribedTypes.Contains(eventType)) continue;

                var url    = webhook.Url;
                var secret = webhook.Secret;

                _jobs.Enqueue<WebhookDeliveryJob>(x =>
                    x.DeliverAsync(url, secret, eventType, jsonPayload, CancellationToken.None));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebhookDispatcher: error dispatching {EventType} for client {ClientId}", eventType, clientId);
        }
    }
}
