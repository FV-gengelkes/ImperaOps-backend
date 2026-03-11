namespace ImperaOps.Domain.Entities;

public sealed class ApiRequestLog
{
    public long Id { get; set; }
    public long? ClientId { get; set; }
    public long? ApiCredentialId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public int LatencyMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
