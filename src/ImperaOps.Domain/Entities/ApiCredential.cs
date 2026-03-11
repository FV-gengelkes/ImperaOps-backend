namespace ImperaOps.Domain.Entities;

public sealed class ApiCredential : ISoftDeletable
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    /// <summary>Human-friendly label, e.g. "Datadog Production Alerts".</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Public key identifier, e.g. "key_7h3k2m9x4p8q".</summary>
    public string KeyId { get; set; } = string.Empty;
    /// <summary>SHA-256 hash of the full secret. Never store plain text.</summary>
    public string SecretHash { get; set; } = string.Empty;
    /// <summary>Last 4 characters of the secret, for display.</summary>
    public string SecretLast4 { get; set; } = string.Empty;
    /// <summary>JSON array of scopes, e.g. ["events:create","events:update"].</summary>
    public string ScopesJson { get; set; } = "[]";
    public string Status { get; set; } = "active";
    public long? CreatedByUserId { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public long? RevokedByUserId { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
