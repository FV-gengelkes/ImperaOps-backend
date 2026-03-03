using FreightVis.Application.Auth.Dtos;

namespace FreightVis.Application.Abstractions;

public interface IJwtService
{
    string GenerateToken(Guid userId, string email, string displayName, bool isSuperAdmin, IReadOnlyList<ClientAccessDto> clients);
}
