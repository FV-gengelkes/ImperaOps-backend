namespace ImperaOps.Domain.Entities;

public sealed class Client : ISoftDeletable
{
    public long Id { get; set; }
    public long? ParentClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    /// <summary>Active | Inactive | Demo | SalesDemo</summary>
    public string Status { get; set; } = "Active";
    /// <summary>JSON array of template IDs applied to this client, e.g. ["tl-safety"].</summary>
    public string? AppliedTemplateIds { get; set; }
    // ── Branding ──────────────────────────────────────────────────────────────
    /// <summary>S3 key for the uploaded logo, e.g. "logos/42".</summary>
    public string? LogoStorageKey { get; set; }
    /// <summary>Hex primary color override, e.g. "#E53E3E".</summary>
    public string? PrimaryColor { get; set; }
    /// <summary>White-label system name shown in the sidebar, e.g. "Acme Safety Hub".</summary>
    public string? SystemName { get; set; }
    /// <summary>Hex color for hyperlinks/text on light surfaces, e.g. "#1A5FB4". Defaults to PrimaryColor.</summary>
    public string? LinkColor { get; set; }
    // ── Inbound Email ──────────────────────────────────────────────────────────
    /// <summary>Unique slug used as the local part of the inbound address, e.g. "acme" → acme@inbound.yourdomain.com. Defaults to Slug.</summary>
    public string? InboundEmailSlug { get; set; }
    /// <summary>Default event type assigned to email-created events. Null = use first active type.</summary>
    public long? DefaultInboundEventTypeId { get; set; }
    /// <summary>Default workflow status assigned to email-created events. Null = use first open status.</summary>
    public long? DefaultInboundWorkflowStatusId { get; set; }
    // ── Public API ────────────────────────────────────────────────────────────
    /// <summary>Stable public SID for API auth, e.g. "csid_29f4a7c1d8ab42e7".</summary>
    public string ClientSid { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
