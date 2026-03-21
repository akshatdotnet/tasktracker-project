using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Domain.Entities;

namespace TaskTracker.Infrastructure.Services;

/// <summary>JWT settings bound from appsettings.json JwtSettings section.</summary>
public sealed class JwtSettings
{
    public string Issuer                   { get; init; } = "TaskTracker.API";
    public string Audience                 { get; init; } = "TaskTracker.Client";
    public string SecretKey                { get; init; } = string.Empty;
    public int    AccessTokenExpiryMinutes { get; init; } = 15;
    public int    RefreshTokenExpiryDays   { get; init; } = 7;
}

/// <summary>
/// JWT access token generation using HS256 (HMAC-SHA256).
/// For production, swap to RS256 by injecting an RSA key pair.
/// Refresh tokens are cryptographically random bytes — not JWTs.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
        => _settings = settings.Value;

    /// <summary>
    /// Generates a signed JWT containing userId, email, and role claims.
    /// Expires in AccessTokenExpiryMinutes (default 15 min).
    /// </summary>
    public string GenerateAccessToken(User user)
    {
        // KeyId must be set on the signing key AND matched in TokenValidationParameters.
        // Without a KeyId the validator throws IDX10517 (kid missing).
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey))
        {
            KeyId = "tasktracker-key-v1"
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Claims embedded in the JWT payload — read by Angular without a DB call
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),  // Unique per token
            new Claim(ClaimTypes.NameIdentifier,      user.Id.ToString()),
            new Claim(ClaimTypes.Email,               user.Email),
            new Claim(ClaimTypes.Role,                user.Role.ToString()),
            new Claim("fullName",                     user.FullName),
        };

        var token = new JwtSecurityToken(
            issuer:             _settings.Issuer,
            audience:           _settings.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically secure opaque refresh token.
    /// 32 bytes = 256 bits of entropy. URL-safe base64 encoded.
    /// The SHA-256 hash of this token is stored in the database.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
