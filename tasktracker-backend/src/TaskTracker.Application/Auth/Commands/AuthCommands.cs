using MediatR;
using Microsoft.Extensions.Logging;
using TaskTracker.Application.Auth.DTOs;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Domain.Common;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskTracker.Domain.Interfaces;

namespace TaskTracker.Application.Auth.Commands;

// ══════════════════════════════════════════════════════════════════════════════
//  LOGIN COMMAND
// ══════════════════════════════════════════════════════════════════════════════

public record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository         _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHashService    _hasher;
    private readonly IJwtTokenService        _jwt;
    private readonly ICurrentUserService     _currentUser;
    private readonly IUnitOfWork             _uow;
    private readonly ILogger<LoginCommandHandler> _log;

    public LoginCommandHandler(
        IUserRepository users, IRefreshTokenRepository refreshTokens,
        IPasswordHashService hasher, IJwtTokenService jwt,
        ICurrentUserService currentUser, IUnitOfWork uow,
        ILogger<LoginCommandHandler> log)
    {
        (_users, _refreshTokens, _hasher, _jwt, _currentUser, _uow, _log)
            = (users, refreshTokens, hasher, jwt, currentUser, uow, log);
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        // OWASP: Never reveal whether email exists — same error message for all failures
        var user = await _users.GetByEmailAsync(cmd.Email, ct);
        if (user is null)
        {
            _log.LogWarning("Login attempt for unknown email {Email} from {IP}", cmd.Email, _currentUser.IpAddress);
            return Result.Failure<LoginResponse>("Invalid credentials.");
        }

        if (!user.IsActive)
            return Result.Failure<LoginResponse>("Invalid credentials.");

        if (user.IsLockedOut())
            return Result.Failure<LoginResponse>("Account is temporarily locked. Please try again in 15 minutes.");

        if (!_hasher.VerifyPassword(cmd.Password, user.PasswordHash, user.Salt))
        {
            user.RecordFailedLogin();
            await _uow.SaveChangesAsync(ct);
            _log.LogWarning("Failed login for {Email} from {IP} (attempt {Count})",
                cmd.Email, _currentUser.IpAddress, user.FailedLoginCount);
            return Result.Failure<LoginResponse>("Invalid credentials.");
        }

        // Success — record login, generate tokens
        user.RecordSuccessfulLogin();

        var accessToken      = _jwt.GenerateAccessToken(user);
        var rawRefreshToken  = _jwt.GenerateRefreshToken();
        var tokenHash        = CryptoHelper.Sha256(rawRefreshToken);
        var refreshToken     = RefreshToken.Create(user.Id, tokenHash, _currentUser.IpAddress);

        await _refreshTokens.AddAsync(refreshToken, ct);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("User {Email} authenticated successfully from {IP}", user.Email, _currentUser.IpAddress);

        return Result.Success(new LoginResponse(
            AccessToken:  accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresAt:    DateTime.UtcNow.AddMinutes(15),
            UserId:       user.Id,
            Role:         user.Role.ToString(),
            FullName:     user.FullName));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  REFRESH TOKEN COMMAND
// ══════════════════════════════════════════════════════════════════════════════

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<LoginResponse>>;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IRefreshTokenRepository _tokens;
    private readonly IUserRepository         _users;
    private readonly IJwtTokenService        _jwt;
    private readonly ICurrentUserService     _currentUser;
    private readonly IUnitOfWork             _uow;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository tokens, IUserRepository users,
        IJwtTokenService jwt, ICurrentUserService currentUser, IUnitOfWork uow)
    {
        (_tokens, _users, _jwt, _currentUser, _uow) = (tokens, users, jwt, currentUser, uow);
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var hash  = CryptoHelper.Sha256(cmd.RefreshToken);
        var token = await _tokens.GetByHashAsync(hash, ct);

        if (token is null || !token.IsActive)
            return Result.Failure<LoginResponse>("Invalid or expired refresh token.");

        var user = await _users.GetByIdAsync(token.UserId, ct);
        if (user is null || !user.IsActive)
            return Result.Failure<LoginResponse>("User not found or deactivated.");

        // ROTATE: revoke old, issue new
        token.Revoke();

        var newRaw   = _jwt.GenerateRefreshToken();
        var newHash  = CryptoHelper.Sha256(newRaw);
        var newToken = RefreshToken.Create(user.Id, newHash, _currentUser.IpAddress);

        await _tokens.AddAsync(newToken, ct);
        await _uow.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(user);
        return Result.Success(new LoginResponse(
            AccessToken:  accessToken,
            RefreshToken: newRaw,
            ExpiresAt:    DateTime.UtcNow.AddMinutes(15),
            UserId:       user.Id,
            Role:         user.Role.ToString(),
            FullName:     user.FullName));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  FORGOT PASSWORD COMMAND
// ══════════════════════════════════════════════════════════════════════════════

public record ForgotPasswordCommand(string Email) : IRequest<Result>;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IUserRepository                  _users;
    private readonly IPasswordResetTokenRepository    _resetTokens;
    private readonly IEmailService                    _email;
    private readonly ICurrentUserService              _currentUser;
    private readonly IUnitOfWork                      _uow;
    private readonly ILogger<ForgotPasswordCommandHandler> _log;

    public ForgotPasswordCommandHandler(
        IUserRepository users, IPasswordResetTokenRepository resetTokens,
        IEmailService email, ICurrentUserService currentUser,
        IUnitOfWork uow, ILogger<ForgotPasswordCommandHandler> log)
    {
        (_users, _resetTokens, _email, _currentUser, _uow, _log)
            = (users, resetTokens, email, currentUser, uow, log);
    }

    public async Task<Result> Handle(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        // OWASP: Always return 200 — never reveal whether email is registered
        var user = await _users.GetByEmailAsync(cmd.Email, ct);
        if (user is null || !user.IsActive)
        {
            _log.LogInformation("Forgot-password for unknown/inactive email {Email}", cmd.Email);
            return Result.Success(); // deliberate no-op
        }

        var rawToken   = CryptoHelper.GenerateSecureToken();
        var hash       = CryptoHelper.Sha256(rawToken);
        var resetToken = PasswordResetToken.Create(user.Id, hash, _currentUser.IpAddress);

        await _resetTokens.AddAsync(resetToken, ct);
        await _uow.SaveChangesAsync(ct);

        // Email service publishes via SMTP or queued event
        var resetLink = $"http://localhost:4200/reset-password?token={rawToken}";
        await _email.SendPasswordResetEmailAsync(user.Email, resetLink, ct);

        _log.LogInformation("Password reset token issued for {UserId}", user.Id);
        return Result.Success();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  RESET PASSWORD COMMAND
// ══════════════════════════════════════════════════════════════════════════════

public record ResetPasswordCommand(string Token, string NewPassword, string ConfirmPassword) : IRequest<Result>;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly IRefreshTokenRepository       _refreshTokens;
    private readonly IUserRepository               _users;
    private readonly IPasswordHashService          _hasher;
    private readonly IUnitOfWork                   _uow;
    private readonly ILogger<ResetPasswordCommandHandler> _log;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository resetTokens, IRefreshTokenRepository refreshTokens,
        IUserRepository users, IPasswordHashService hasher,
        IUnitOfWork uow, ILogger<ResetPasswordCommandHandler> log)
    {
        (_resetTokens, _refreshTokens, _users, _hasher, _uow, _log)
            = (resetTokens, refreshTokens, users, hasher, uow, log);
    }

    public async Task<Result> Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var hash       = CryptoHelper.Sha256(cmd.Token);
        var resetToken = await _resetTokens.GetByHashAsync(hash, ct);

        if (resetToken is null || !resetToken.IsValid)
            return Result.Failure("Reset token is invalid or has expired.");

        var user = await _users.GetByIdAsync(resetToken.UserId, ct);
        if (user is null)
            return Result.Failure("User not found.");

        var newHash = _hasher.HashPassword(cmd.NewPassword, out var salt);
        user.UpdatePassword(newHash, salt);
        resetToken.MarkUsed();

        // Revoke ALL refresh tokens — forces re-login on all devices
        await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("Password reset completed for user {UserId}", user.Id);
        return Result.Success();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  REGISTER COMMAND (Admin only)
// ══════════════════════════════════════════════════════════════════════════════

public record RegisterCommand(
    string Email, string Password, string ConfirmPassword,
    string FirstName, string LastName, string Role) : IRequest<Result<UserDto>>;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<UserDto>>
{
    private readonly IUserRepository      _users;
    private readonly IPasswordHashService _hasher;
    private readonly IUnitOfWork          _uow;

    public RegisterCommandHandler(IUserRepository users, IPasswordHashService hasher, IUnitOfWork uow)
        => (_users, _hasher, _uow) = (users, hasher, uow);

    public async Task<Result<UserDto>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        if (await _users.ExistsAsync(cmd.Email, ct))
            return Result.Failure<UserDto>("A user with this email already exists.");

        if (!Enum.TryParse<UserRole>(cmd.Role, ignoreCase: true, out var role))
            return Result.Failure<UserDto>($"Invalid role '{cmd.Role}'. Valid values: Admin, Developer, Viewer.");

        var hash = _hasher.HashPassword(cmd.Password, out var salt);
        var user = User.Create(cmd.Email, hash, salt, cmd.FirstName, cmd.LastName, role);

        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new UserDto(user.Id, user.Email, user.FullName,
            user.Role.ToString(), user.IsActive, user.CreatedAt, user.LastLoginAt));
    }
}

// ── Shared Crypto Helper ──────────────────────────────────────────────────────

internal static class CryptoHelper
{
    /// <summary>SHA-256 hex string of the input — used for token hashing.</summary>
    public static string Sha256(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    /// <summary>Cryptographically random 32-byte URL-safe base64 token.</summary>
    public static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
