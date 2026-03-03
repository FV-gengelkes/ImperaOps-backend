namespace FreightVis.Api.Contracts;

// ── Clients ──────────────────────────────────────────────────────────────────

public sealed record AdminClientDto(
    Guid    Id,
    string  Name,
    Guid?   ParentClientId,
    string? ParentClientName,
    bool    IsActive,
    int     UserCount,
    DateTimeOffset CreatedAt
);

public sealed record CreateClientRequest(string Name, Guid? ParentClientId);

public sealed record UpdateClientRequest(string Name, Guid? ParentClientId, bool IsActive);

// ── Users ─────────────────────────────────────────────────────────────────────

public sealed record AdminUserDto(
    Guid   Id,
    string Email,
    string DisplayName,
    bool   IsActive,
    bool   IsSuperAdmin,
    int    ClientCount,
    DateTimeOffset CreatedAt
);

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    bool   IsSuperAdmin
);

public sealed record UpdateUserRequest(
    string Email,
    string DisplayName,
    bool   IsActive,
    bool   IsSuperAdmin
);

public sealed record ChangePasswordRequest(string NewPassword);

// ── User–Client access ────────────────────────────────────────────────────────

public sealed record UserClientAccessDto(
    Guid   ClientId,
    string ClientName,
    bool   ClientIsActive,
    string Role,
    DateTimeOffset GrantedAt
);

public sealed record GrantClientAccessRequest(Guid ClientId, string Role);
