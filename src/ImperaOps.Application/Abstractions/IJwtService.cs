using ImperaOps.Application.Auth.Dtos;

namespace ImperaOps.Application.Abstractions;

public interface IJwtService
{
    string GenerateToken(long userId, string email, string displayName, bool isSuperAdmin, IReadOnlyList<ClientAccessDto> clients, string? sessionId = null);
}
