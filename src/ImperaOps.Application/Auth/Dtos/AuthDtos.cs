namespace ImperaOps.Application.Auth.Dtos;

public sealed record LoginCommand(string Email, string Password);

public sealed record AuthResultDto(
    string Token,
    string DisplayName,
    string Email,
    bool IsSuperAdmin,
    IReadOnlyList<ClientAccessDto> Clients
);

/// <summary>Intermediate result from LoginHandler — credentials without a JWT (controller builds the JWT after creating a session).</summary>
public sealed record LoginCredentialsDto(
    long UserId,
    string Email,
    string DisplayName,
    bool IsSuperAdmin,
    IReadOnlyList<ClientAccessDto> Clients
);

public sealed record ClientAccessDto(
    long Id,
    string Name,
    string Role,
    long? ParentClientId
);
