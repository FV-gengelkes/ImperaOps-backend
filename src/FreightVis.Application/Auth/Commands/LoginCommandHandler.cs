using FreightVis.Application.Abstractions;
using FreightVis.Application.Auth.Dtos;
using MediatR;

namespace FreightVis.Application.Auth.Commands;

public sealed class LoginRequest : IRequest<AuthResultDto>
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginHandler : IRequestHandler<LoginRequest, AuthResultDto>
{
    private readonly IAuthRepository _auth;
    private readonly IJwtService _jwt;

    public LoginHandler(IAuthRepository auth, IJwtService jwt)
    {
        _auth = auth;
        _jwt = jwt;
    }

    public async Task<AuthResultDto> Handle(LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.ValidateAsync(request.Email, request.Password, ct)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        var (userId, email, displayName, isSuperAdmin, clients) = result;

        var token = _jwt.GenerateToken(userId, email, displayName, isSuperAdmin, clients);

        return new AuthResultDto(token, displayName, email, isSuperAdmin, clients);
    }
}
