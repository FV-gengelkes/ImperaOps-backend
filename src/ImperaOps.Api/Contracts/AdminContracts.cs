namespace ImperaOps.Api.Contracts;

// ── Clients ──────────────────────────────────────────────────────────────────

public sealed record AdminClientDto(
    long   Id,
    string Name,
    string Slug,
    long?  ParentClientId,
    string? ParentClientName,
    bool   IsActive,
    int    UserCount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> AppliedTemplateIds
);

public sealed record CreateClientRequest(string Name, long? ParentClientId, string? TemplateId = null);

public sealed record UpdateClientRequest(string Name, long? ParentClientId, bool IsActive);

// ── Users ─────────────────────────────────────────────────────────────────────

public sealed record AdminUserDto(
    long   Id,
    string Email,
    string DisplayName,
    bool   IsActive,
    bool   IsSuperAdmin,
    int    ClientCount,
    DateTimeOffset CreatedAt,
    bool   IsTotpEnabled = false
);

public sealed record CreateUserRequest(
    string  Email,
    string  DisplayName,
    bool    IsSuperAdmin,
    long?   ClientId      = null,    // client to associate on creation
    string? Role          = "Member", // role within that client
    long?   AuditClientId = null      // fallback audit scope (used for super-admin creation)
);

public sealed record UpdateUserRequest(
    string Email,
    string DisplayName,
    bool   IsActive,
    bool   IsSuperAdmin,
    long?  AuditClientId = null  // current client context for audit scoping
);

public sealed record ChangePasswordRequest(string NewPassword);

// ── User–Client access ────────────────────────────────────────────────────────

public sealed record UserClientAccessDto(
    long   ClientId,
    string ClientName,
    bool   ClientIsActive,
    string Role,
    DateTimeOffset GrantedAt
);

public sealed record GrantClientAccessRequest(long ClientId, string Role);

public sealed record AdminClientUserDto(
    long   UserId,
    string DisplayName,
    string Email,
    string Role,
    bool   IsActive,
    bool   IsSuperAdmin
);

public sealed record UpdateClientUserRoleRequest(string Role);

/// <summary>Returned from any endpoint that creates a user and sends an invite email.</summary>
public sealed record InviteUserResult<T>(T User, string InviteUrl, bool EmailSent);

// ── Branding ──────────────────────────────────────────────────────────────────

public sealed record ClientBrandingDto(
    string? SystemName,
    string? PrimaryColor,
    string? LinkColor,
    string? LogoUrl          // presigned URL, null if no logo
);

public sealed record UpdateBrandingRequest(string? SystemName, string? PrimaryColor, string? LinkColor = null);

// ── Inbound Email ─────────────────────────────────────────────────────────────

public sealed record ClientInboundEmailDto(
    string? InboundEmailSlug,
    string? InboundAddress,
    long?   DefaultInboundEventTypeId,
    long?   DefaultInboundWorkflowStatusId,
    IReadOnlyList<InboundEventTypeOption>    EventTypes,
    IReadOnlyList<InboundWorkflowStatusOption> WorkflowStatuses
);

public sealed record InboundEventTypeOption(long Id, string Name);
public sealed record InboundWorkflowStatusOption(long Id, string Name);

public sealed record UpdateClientInboundEmailRequest(
    string? InboundEmailSlug,
    long?   DefaultInboundEventTypeId,
    long?   DefaultInboundWorkflowStatusId
);

// ── Event Templates ───────────────────────────────────────────────────────────

public sealed record EventTemplateListItemDto(
    string Id,
    string Name,
    string Description,
    string Industry,
    int    EventTypeCount,
    int    StatusCount,
    int    CustomFieldCount
);

// ── SLA Rules ─────────────────────────────────────────────────────────────────

public sealed record SlaRuleDto(
    long    Id,
    long?   EventTypeId,
    string? EventTypeName,
    string  Name,
    int?    InvestigationHours,
    int?    ClosureHours
);

public sealed record CreateSlaRuleRequest(
    long?   EventTypeId,
    string  Name,
    int?    InvestigationHours,
    int?    ClosureHours
);

/// <summary>Audit event DTO enriched with the client name for the admin audit log.</summary>
public sealed record AdminAuditEventDto(
    long     Id,
    long     ClientId,
    string?  ClientName,
    string   EntityType,
    long     EntityId,
    string   EventType,
    long?    UserId,
    string   UserDisplayName,
    string   Body,
    DateTimeOffset CreatedAt
);
