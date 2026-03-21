using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Infrastructure.Services;

// ── Password Hash Service ─────────────────────────────────────────────────────

/// <summary>
/// bcrypt password hashing with configurable work factor.
/// Work factor 12 = ~250ms per hash — too slow for brute-force, fast enough for UX.
/// </summary>
public sealed class PasswordHashService : IPasswordHashService
{
    private readonly int _workFactor;

    public PasswordHashService(IOptions<SecuritySettings> opts)
        => _workFactor = opts.Value.BcryptWorkFactor;

    /// <summary>Hashes password and returns the hash. The salt is embedded inside the hash by bcrypt.</summary>
    public string HashPassword(string password, out string salt)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: _workFactor);
        salt = hash; // bcrypt embeds salt in the hash string — we store it as "salt" for schema compat
        return hash;
    }

    /// <summary>Verifies a plain-text password against the stored bcrypt hash.</summary>
    public bool VerifyPassword(string password, string hash, string salt)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }
}

public sealed class SecuritySettings
{
    public int BcryptWorkFactor { get; init; } = 12;
}

// ── Memory Cache Service ──────────────────────────────────────────────────────

/// <summary>
/// IMemoryCache wrapper implementing ICacheService.
/// In production, swap this for a Redis implementation — no handler changes needed.
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(2)
        };
        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        // IMemoryCache doesn't support prefix removal — use CompactAsync in production Redis
        // For dev purposes, this is a no-op
        return Task.CompletedTask;
    }
}

// ── Email Service (Console/Log implementation for local dev) ──────────────────

/// <summary>
/// Local development email service — writes to logs instead of sending real emails.
/// Replace with SMTP or SendGrid implementation for production.
/// </summary>
public sealed class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger) => _logger = logger;

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[EMAIL - DEV MODE] Password reset for {Email}. Link: {ResetLink}",
            toEmail, resetLink);
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL - DEV MODE] Welcome email for {Name} <{Email}>", firstName, toEmail);
        return Task.CompletedTask;
    }
}

// ── DateTime Service ──────────────────────────────────────────────────────────

/// <summary>System clock implementation. Override in tests with a fixed date.</summary>
public sealed class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Today  => DateTime.UtcNow.Date;
}

// ── Current User Service (reads from JWT claims) ──────────────────────────────

/// <summary>
/// Reads the authenticated user's identity from the current HTTP context claims.
/// Populated by the JWT Bearer middleware from the Authorization header.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public Guid? UserId
    {
        get
        {
            var claim = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? Email  => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
    public bool    IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public UserRole? Role
    {
        get
        {
            var claim = _http.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(claim, out var role) ? role : null;
        }
    }

    public string? IpAddress =>
        _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
