using ImperaOps.Application.Auth.Dtos;

namespace ImperaOps.Application.Abstractions;

public interface IAuthRepository
{
    /// <summary>Returns null if credentials are invalid.</summary>
    Task<(long UserId, string Email, string DisplayName, bool IsSuperAdmin, IReadOnlyList<ClientAccessDto> Clients)?> ValidateAsync(
        string email, string password, CancellationToken ct);
}
