using ImperaOps.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ImperaOps.Api.Health;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly IStorageService _storage;

    public StorageHealthCheck(IStorageService storage)
        => _storage = storage;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _storage.EnsureBucketExistsAsync(cancellationToken);
            return HealthCheckResult.Healthy("Object storage accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Object storage check failed", ex);
        }
    }
}
