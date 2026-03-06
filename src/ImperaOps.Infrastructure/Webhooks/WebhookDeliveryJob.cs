using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Webhooks;

public class WebhookDeliveryJob
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryJob> _logger;

    public WebhookDeliveryJob(IHttpClientFactory httpClientFactory, ILogger<WebhookDeliveryJob> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task DeliverAsync(string url, string? secret, string eventType, string payload, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("WebhookClient");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("X-ImperaOps-Event", eventType);

        if (!string.IsNullOrEmpty(secret))
        {
            var hmac      = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            request.Headers.TryAddWithoutValidation("X-ImperaOps-Signature", $"sha256={signature}");
        }

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Webhook delivery to {Url} failed with {Status}: {Body}", url, response.StatusCode, body);
            throw new HttpRequestException($"Webhook delivery failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        _logger.LogDebug("Webhook delivered to {Url} ({EventType})", url, eventType);
    }
}
