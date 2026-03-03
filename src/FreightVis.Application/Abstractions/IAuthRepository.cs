using FreightVis.Application.Auth.Dtos;

namespace FreightVis.Application.Abstractions;

public interface IAuthRepository
{
    /// <summary>Returns null if credentials are invalid.</summary>
    Task<(Guid UserId, string Email, string DisplayName, bool IsSuperAdmin, IReadOnlyList<ClientAccessDto> Clients)?> ValidateAsync(
        string email, string password, CancellationToken ct);
}
