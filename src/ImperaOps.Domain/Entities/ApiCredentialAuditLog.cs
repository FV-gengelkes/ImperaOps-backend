namespace ImperaOps.Domain.Entities;

public sealed class ApiCredentialAuditLog
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public long ApiCredentialId { get; set; }
    /// <summary>"created" | "revoked" | "rotated" | "updated"</summary>
    public string Action { get; set; } = string.Empty;
    public long? PerformedByUserId { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
