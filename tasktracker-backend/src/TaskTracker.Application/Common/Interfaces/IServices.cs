using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Common.Interfaces;

/// <summary>JWT access token generation and refresh token creation.</summary>
public interface IJwtTokenService
{
    /// <summary>Generates a signed JWT access token valid for 15 minutes.</summary>
    string GenerateAccessToken(User user);

    /// <summary>Generates a cryptographically random opaque refresh token (base64url).</summary>
    string GenerateRefreshToken();
}

/// <summary>bcrypt password hashing — work factor 12.</summary>
public interface IPasswordHashService
{
    /// <summary>Hashes a plain-text password. Returns the hash; outputs the salt.</summary>
    string HashPassword(string password, out string salt);

    /// <summary>Verifies a plain-text password against stored hash + salt.</summary>
    bool VerifyPassword(string password, string hash, string salt);
}

/// <summary>Ambient context for the currently authenticated HTTP user.</summary>
public interface ICurrentUserService
{
    Guid?     UserId        { get; }
    string?   Email         { get; }
    UserRole? Role          { get; }
    bool      IsAuthenticated { get; }
    string?   IpAddress     { get; }
}

/// <summary>Email notification service — implementation lives in Infrastructure.</summary>
public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink,    CancellationToken ct = default);
    Task SendWelcomeEmailAsync      (string toEmail, string firstName,    CancellationToken ct = default);
}

/// <summary>
/// Generic distributed/in-memory cache abstraction.
/// Concrete: MemoryCache (dev) or Redis (prod).
/// </summary>
public interface ICacheService
{
    Task<T?>  GetAsync<T>         (string key, CancellationToken ct = default) where T : class;
    Task      SetAsync<T>         (string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task      RemoveAsync         (string key, CancellationToken ct = default);
    Task      RemoveByPrefixAsync (string prefix, CancellationToken ct = default);
}

/// <summary>DateTime provider — makes handlers unit-testable.</summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateTime Today  { get; }
}
