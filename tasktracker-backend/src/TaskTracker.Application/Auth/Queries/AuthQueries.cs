using MediatR;
using TaskTracker.Application.Auth.DTOs;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Domain.Common;
using TaskTracker.Domain.Interfaces;

namespace TaskTracker.Application.Auth.Queries;

// ── Validate Reset Token ───────────────────────────────────────────────────────

public record ValidateResetTokenQuery(string Token) : IRequest<Result<ValidateResetTokenResponse>>;

public sealed class ValidateResetTokenQueryHandler
    : IRequestHandler<ValidateResetTokenQuery, Result<ValidateResetTokenResponse>>
{
    private readonly IPasswordResetTokenRepository _tokens;

    public ValidateResetTokenQueryHandler(IPasswordResetTokenRepository tokens)
        => _tokens = tokens;

    public async Task<Result<ValidateResetTokenResponse>> Handle(
        ValidateResetTokenQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Token))
            return Result.Success(new ValidateResetTokenResponse(false, null));

        var hash  = Sha256(query.Token);
        var token = await _tokens.GetByHashAsync(hash, ct);

        return token is null || !token.IsValid
            ? Result.Success(new ValidateResetTokenResponse(false, null))
            : Result.Success(new ValidateResetTokenResponse(true, token.ExpiresAt));
    }

    private static string Sha256(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input)));
    }
}

// ── Get Current User ───────────────────────────────────────────────────────────

public record GetCurrentUserQuery : IRequest<Result<UserDto>>;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly IUserRepository     _users;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserQueryHandler(IUserRepository users, ICurrentUserService currentUser)
        => (_users, _currentUser) = (users, currentUser);

    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery _, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result.Failure<UserDto>("Not authenticated.");

        var user = await _users.GetByIdAsync(_currentUser.UserId.Value, ct);
        if (user is null)
            return Result.Failure<UserDto>("User not found.");

        return Result.Success(new UserDto(
            user.Id, user.Email, user.FullName,
            user.Role.ToString(), user.IsActive,
            user.CreatedAt, user.LastLoginAt));
    }
}
