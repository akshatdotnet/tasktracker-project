using TaskTracker.MVC.Application.DTOs.Auth;
using TaskTracker.MVC.Application.Interfaces;

namespace TaskTracker.MVC.Infrastructure.Services.Mock;

/// <summary>
/// In-memory mock auth service — no HTTP calls, no API needed.
/// Mirrors the Angular AuthMockService exactly:
///   admin@dev.local      / Admin@1234  → Admin
///   dev@dev.local        / Dev@12345   → Developer
///   viewer@dev.local     / Viewer@123  → Viewer
///   admin@tasktracker.dev / Admin@1234 → Admin   (real API seeds)
///   arjun@tasktracker.dev / Dev@12345  → Developer
///   priya@tasktracker.dev / Dev@12345  → Developer
///   viewer@tasktracker.dev / Dev@12345 → Viewer
/// Activated when ApiSettings.UseMockData = true in appsettings.json.
/// </summary>
public sealed class MockAuthService : IAuthService
{
    private static readonly List<MockUser> Users = new()
    {
        // Mock credentials (same as Angular mock service)
        new("admin@dev.local",        "Admin@1234", "Admin",     "u-0001", "System Admin"),
        new("dev@dev.local",          "Dev@12345",  "Developer", "u-0002", "Dev User"),
        new("viewer@dev.local",       "Viewer@123", "Viewer",    "u-0003", "Viewer User"),
        // Real API seeds also work in mock mode
        new("admin@tasktracker.dev",  "Admin@1234", "Admin",     "u-0004", "System Admin"),
        new("arjun@tasktracker.dev",  "Dev@12345",  "Developer", "u-0005", "Arjun Mehta"),
        new("priya@tasktracker.dev",  "Dev@12345",  "Developer", "u-0006", "Priya Sharma"),
        new("rohan@tasktracker.dev",  "Dev@12345",  "Developer", "u-0007", "Rohan Desai"),
        new("viewer@tasktracker.dev", "Dev@12345",  "Viewer",    "u-0008", "Client Viewer"),
    };

    public Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var user = Users.FirstOrDefault(u =>
            u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) &&
            u.Password == request.Password);

        if (user is null) return Task.FromResult<LoginResponseDto?>(null);

        var response = new LoginResponseDto
        {
            AccessToken  = $"mock.jwt.{user.Role.ToLower()}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            RefreshToken = $"mock-refresh-{user.UserId}-{Guid.NewGuid():N}",
            ExpiresAt    = DateTime.UtcNow.AddMinutes(15),
            UserId       = user.UserId,
            Role         = user.Role,
            FullName     = user.FullName
        };

        return Task.FromResult<LoginResponseDto?>(response);
    }

    public Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        // Mock: always return a new valid token for Developer
        var response = new LoginResponseDto
        {
            AccessToken  = $"mock.jwt.refreshed.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            RefreshToken = $"mock-refresh-rotated-{Guid.NewGuid():N}",
            ExpiresAt    = DateTime.UtcNow.AddMinutes(15),
            UserId       = "u-0002",
            Role         = "Developer",
            FullName     = "Dev User"
        };
        return Task.FromResult<LoginResponseDto?>(response);
    }

    public Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken ct = default)
        => Task.CompletedTask; // Mock: silently succeed (OWASP no-enum)

    public Task<ValidateTokenResponseDto> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        // Mock: any non-empty token is valid
        var result = new ValidateTokenResponseDto
        {
            IsValid   = !string.IsNullOrEmpty(token),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        return Task.FromResult(result);
    }

    public Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken ct = default)
    {
        // Mock: succeed if passwords match
        return Task.FromResult(request.NewPassword == request.ConfirmPassword);
    }

    private sealed record MockUser(string Email, string Password, string Role, string UserId, string FullName);
}
