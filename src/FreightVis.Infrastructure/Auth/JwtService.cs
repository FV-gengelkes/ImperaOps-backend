using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FreightVis.Application.Abstractions;
using FreightVis.Application.Auth.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FreightVis.Infrastructure.Auth;

public sealed class JwtService : IJwtService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtService(IConfiguration config)
    {
        _key          = config["Jwt:Key"]          ?? throw new InvalidOperationException("Missing Jwt:Key");
        _issuer       = config["Jwt:Issuer"]       ?? "freightvis-api";
        _audience     = config["Jwt:Audience"]     ?? "freightvis-app";
        _expiryMinutes = int.Parse(config["Jwt:ExpiryMinutes"] ?? "480");
    }

    public string GenerateToken(Guid userId, string email, string displayName, bool isSuperAdmin, IReadOnlyList<ClientAccessDto> clients)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("display_name",                displayName),
            new("is_super_admin",              isSuperAdmin ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        // Embed accessible client IDs as claims so the API can scope data per client
        foreach (var c in clients)
            claims.Add(new Claim("client_id", c.Id.ToString()));

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:   _issuer,
            audience: _audience,
            claims:   claims,
            expires:  DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
