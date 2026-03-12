using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ImperaOps.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly HealthCheckService _health;

    // Captured once at startup — changes only when a new image is deployed.
    private static readonly string BuildTimestamp =
        System.IO.File.Exists("/app/build-id")
            ? System.IO.File.ReadAllText("/app/build-id").Trim()
            : DateTimeOffset.UtcNow.ToString("o");

    public HealthController(HealthCheckService health)
        => _health = health;

    /// <summary>Aggregate health — all registered checks.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var report = await _health.CheckHealthAsync(ct);
        var status = report.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(status, new
        {
            status  = report.Status.ToString(),
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status      = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    error       = e.Value.Exception?.Message,
                })
        });
    }

    /// <summary>Returns the build version for deployment detection.</summary>
    [HttpGet("version")]
    public IActionResult Version() => Ok(new { buildId = BuildTimestamp });

    /// <summary>Database connectivity check.</summary>
    [HttpGet("db")]
    public async Task<IActionResult> Db(CancellationToken ct)
        => await RunCheck("database", ct);

    /// <summary>Object storage check.</summary>
    [HttpGet("storage")]
    public async Task<IActionResult> Storage(CancellationToken ct)
        => await RunCheck("storage", ct);

    private async Task<IActionResult> RunCheck(string name, CancellationToken ct)
    {
        var report = await _health.CheckHealthAsync(r => r.Name == name, ct);
        var httpStatus = report.Status == HealthStatus.Healthy ? 200 : 503;
        var entry = report.Entries.TryGetValue(name, out var e) ? (HealthReportEntry?)e : null;
        return StatusCode(httpStatus, new
        {
            status      = report.Status.ToString(),
            description = entry?.Description,
            error       = entry?.Exception?.Message,
        });
    }
}
