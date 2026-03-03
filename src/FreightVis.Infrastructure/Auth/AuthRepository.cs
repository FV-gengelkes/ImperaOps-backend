using FreightVis.Application.Abstractions;
using FreightVis.Application.Auth.Dtos;
using FreightVis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FreightVis.Infrastructure.Auth;

public sealed class AuthRepository : IAuthRepository
{
    private readonly FreightVisDbContext _db;

    public AuthRepository(FreightVisDbContext db) => _db = db;

    public async Task<(Guid UserId, string Email, string DisplayName, bool IsSuperAdmin, IReadOnlyList<ClientAccessDto> Clients)?> ValidateAsync(
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
            .Join(_db.Clients.Where(c => c.IsActive),
                  a => a.ClientId,
                  c => c.Id,
                  (a, c) => new ClientAccessDto(c.Id, c.Name, a.Role, c.ParentClientId))
            .ToListAsync(ct);

        return (user.Id, user.Email, user.DisplayName, user.IsSuperAdmin, clients);
    }
}
