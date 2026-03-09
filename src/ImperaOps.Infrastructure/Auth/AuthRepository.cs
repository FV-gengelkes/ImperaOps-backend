using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Auth.Dtos;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Auth;

public sealed class AuthRepository : IAuthRepository
{
    private readonly ImperaOpsDbContext _db;

    public AuthRepository(ImperaOpsDbContext db) => _db = db;

    public async Task<(long UserId, string Email, string DisplayName, bool IsSuperAdmin, IReadOnlyList<ClientAccessDto> Clients)?> ValidateAsync(
        string email, string password, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        var clients = await _db.UserClientAccess
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .Join(_db.Clients.Where(c => c.Status != "Inactive"),
                  a => a.ClientId,
                  c => c.Id,
                  (a, c) => new ClientAccessDto(c.Id, c.Name, a.Role, c.ParentClientId))
            .ToListAsync(ct);

        return (user.Id, user.Email, user.DisplayName, user.IsSuperAdmin, clients);
    }
}
