namespace TaskTracker.Application.Auth.DTOs;

// ── Request DTOs (bound from HTTP request body) ───────────────────────────────

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ValidateResetTokenRequest(string Token);

public record ResetPasswordRequest(
    string Token,
    string NewPassword,
    string ConfirmPassword);

public record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string FirstName,
    string LastName,
    string Role = "Developer");

// ── Response DTOs (serialized to HTTP response body) ─────────────────────────

public record LoginResponse(
    string   AccessToken,
    string   RefreshToken,
    DateTime ExpiresAt,
    Guid     UserId,
    string   Role,
    string   FullName);

public record ValidateResetTokenResponse(
    bool      IsValid,
    DateTime? ExpiresAt);

public record UserDto(
    Guid   Id,
    string Email,
    string FullName,
    string Role,
    bool   IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
