using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediBook.Auth.API.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MediBook.Auth.API.Helpers;

/// <summary>
/// Generates signed JWT access tokens.
/// Uses HMAC-SHA256 signing with the configured secret key.
/// </summary>
public sealed class JwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    /// <summary>
    /// Creates a signed JWT access token for the given user.
    /// Claims included: sub (userId), email, role, jti (unique token id).
    /// </summary>
    public string GenerateAccessToken(User user)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        // Standard + custom claims
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),  // unique per token
            new Claim(ClaimTypes.Role,               user.Role),
            new Claim("fullName",                    user.FullName),
        };

        var token = new JwtSecurityToken(
            issuer:             _settings.Issuer,
            audience:           _settings.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
