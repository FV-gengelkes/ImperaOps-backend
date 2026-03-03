namespace FreightVis.Application.Auth.Dtos;

public sealed record LoginCommand(string Email, string Password);

public sealed record AuthResultDto(
    string Token,
    string DisplayName,
    string Email,
    bool IsSuperAdmin,
    IReadOnlyList<ClientAccessDto> Clients
);

public sealed record ClientAccessDto(
    Guid Id,
    string Name,
    string Role,
    Guid? ParentClientId
);
