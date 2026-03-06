using ImperaOps.Application.Abstractions;
using ImperaOps.Application.Auth.Dtos;
using MediatR;

namespace ImperaOps.Application.Auth.Commands;

public sealed class LoginRequest : IRequest<LoginCredentialsDto>
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginHandler : IRequestHandler<LoginRequest, LoginCredentialsDto>
{
    private readonly IAuthRepository _auth;

    public LoginHandler(IAuthRepository auth) => _auth = auth;

    public async Task<LoginCredentialsDto> Handle(LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.ValidateAsync(request.Email, request.Password, ct)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        var (userId, email, displayName, isSuperAdmin, clients) = result;

        return new LoginCredentialsDto(userId, email, displayName, isSuperAdmin, clients);
    }
}
