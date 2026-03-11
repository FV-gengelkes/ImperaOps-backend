namespace ImperaOps.Api.Contracts;

public sealed record CreateApiCredentialRequest(
    string Name,
    string[] Scopes,
    DateTimeOffset? ExpiresAt = null
);

public sealed record UpdateApiCredentialRequest(
    string Name,
    string[] Scopes,
    bool IsActive,
    DateTimeOffset? ExpiresAt = null
);

public sealed record ApiCredentialDto(
    long Id,
    long ClientId,
    string ClientSid,
    string Name,
    string KeyId,
    string SecretLast4,
    string[] Scopes,
    string Status,
    string? LastUsedAt,
    string? LastUsedIp,
    string? ExpiresAt,
    string CreatedAt
);

/// <summary>Returned only once at creation time.</summary>
public sealed record ApiCredentialCreatedDto(
    long Id,
    string ClientSid,
    string KeyId,
    string Secret,
    string Name,
    string[] Scopes,
    string AuthorizationHeader
);

public sealed record ApiCredentialAuditLogDto(
    long Id,
    string Action,
    long? PerformedByUserId,
    string? DetailsJson,
    string CreatedAt
);
