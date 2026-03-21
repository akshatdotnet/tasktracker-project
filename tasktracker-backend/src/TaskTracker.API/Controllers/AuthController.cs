using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Auth.Commands;
using TaskTracker.Application.Auth.DTOs;
using TaskTracker.Application.Auth.Queries;
using TaskTracker.Application.Common.Models;

namespace TaskTracker.API.Controllers;

/// <summary>
/// Authentication endpoints — all public (no [Authorize]) except /me.
/// Rate limited: 10 req/min for login and forgot-password (OWASP brute-force protection).
/// Returns uniform ApiResponse wrapping for all success responses.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    // ── POST /api/v1/auth/login ────────────────────────────────────────────

    /// <summary>Authenticate user and issue JWT access + refresh token pair.</summary>
    /// <response code="200">Login successful — tokens returned.</response>
    /// <response code="400">Validation failed (missing/invalid fields).</response>
    /// <response code="401">Invalid credentials or account locked.</response>
    /// <response code="429">Rate limit exceeded.</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse),  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse),  StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(req.Email, req.Password), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error!.Contains("locked")
                ? StatusCode(StatusCodes.Status429TooManyRequests, new ErrorResponse(result.Error!, "ACCOUNT_LOCKED"))
                : Unauthorized(new ErrorResponse(result.Error!, "INVALID_CREDENTIALS"));
    }

    // ── POST /api/v1/auth/refresh-token ───────────────────────────────────

    /// <summary>Rotate refresh token and issue new access + refresh token pair.</summary>
    /// <response code="200">Token rotated successfully.</response>
    /// <response code="401">Refresh token invalid, expired, or revoked.</response>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse),  StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(req.RefreshToken), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new ErrorResponse(result.Error!, "INVALID_TOKEN"));
    }

    // ── POST /api/v1/auth/forgot-password ─────────────────────────────────

    /// <summary>
    /// Request a password reset email.
    /// Always returns 200 — never reveals if the email is registered (OWASP no-enum).
    /// </summary>
    /// <response code="200">Request processed (email sent if account exists).</response>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest req,
        CancellationToken ct)
    {
        await _mediator.Send(new ForgotPasswordCommand(req.Email), ct);
        // Always return 200 — OWASP no email enumeration
        return Ok(new { message = "If that email address is registered, you will receive a reset link shortly." });
    }

    // ── POST /api/v1/auth/validate-token ──────────────────────────────────

    /// <summary>Check if a password reset token is still valid (before showing the reset form).</summary>
    /// <response code="200">Token validity status returned.</response>
    [HttpPost("validate-token")]
    [ProducesResponseType(typeof(ValidateResetTokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateToken(
        [FromBody] ValidateResetTokenRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ValidateResetTokenQuery(req.Token), ct);
        return Ok(result.Value);
    }

    // ── POST /api/v1/auth/reset-password ──────────────────────────────────

    /// <summary>Set a new password using a valid reset token. Revokes all sessions.</summary>
    /// <response code="200">Password reset successfully.</response>
    /// <response code="400">Token invalid/expired, or passwords don't match.</response>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ResetPasswordCommand(req.Token, req.NewPassword, req.ConfirmPassword), ct);

        return result.IsSuccess
            ? Ok(new { message = "Password reset successfully. Please sign in with your new password." })
            : BadRequest(new ErrorResponse(result.Error!, "RESET_FAILED"));
    }

    // ── POST /api/v1/auth/register ────────────────────────────────────────

    /// <summary>Register a new user account. Admin role required.</summary>
    /// <response code="201">User created successfully.</response>
    /// <response code="400">Validation failed or email already exists.</response>
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RegisterCommand(req.Email, req.Password, req.ConfirmPassword,
                req.FirstName, req.LastName, req.Role), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetCurrentUser), new { }, result.Value)
            : BadRequest(new ErrorResponse(result.Error!, "REGISTER_FAILED"));
    }

    // ── GET /api/v1/auth/me ───────────────────────────────────────────────

    /// <summary>Get the currently authenticated user's profile.</summary>
    /// <response code="200">Current user profile.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new ErrorResponse(result.Error!, "NOT_AUTHENTICATED"));
    }
}
