using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaskTracker.MVC.Application.DTOs.Auth;
using TaskTracker.MVC.Application.Interfaces;

namespace TaskTracker.MVC.Infrastructure.Services;

/// <summary>
/// Calls the existing TaskTracker .NET Core 8 API auth endpoints.
/// DIP: implements IAuthService — Web layer depends on the interface, not this class.
/// SRP: only handles HTTP calls to /api/v1/auth/* endpoints.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ILogger<AuthService> _log;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthService(HttpClient http, ILogger<AuthService> log)
    {
        _http = http;
        _log  = log;
    }

    /// <summary>POST /api/v1/auth/login</summary>
    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("auth/login", new
            {
                email    = request.Email,
                password = request.Password
            }, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _log.LogWarning("Login failed [{Status}]: {Error}", (int)response.StatusCode, error);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LoginResponseDto>(_json, ct);
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "API connection failed during login");
            return null;
        }
    }

    /// <summary>POST /api/v1/auth/refresh-token</summary>
    public async Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("auth/refresh-token",
                new { refreshToken }, ct);

            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LoginResponseDto>(_json, ct);
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Token refresh failed");
            return null;
        }
    }

    /// <summary>POST /api/v1/auth/forgot-password — always returns 200 (OWASP)</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken ct = default)
    {
        try
        {
            await _http.PostAsJsonAsync("auth/forgot-password", new { email = request.Email }, ct);
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Forgot-password request failed");
        }
    }

    /// <summary>POST /api/v1/auth/validate-token</summary>
    public async Task<ValidateTokenResponseDto> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("auth/validate-token", new { token }, ct);
            if (!response.IsSuccessStatusCode) return new ValidateTokenResponseDto { IsValid = false };
            return await response.Content.ReadFromJsonAsync<ValidateTokenResponseDto>(_json, ct)
                   ?? new ValidateTokenResponseDto { IsValid = false };
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Token validation failed");
            return new ValidateTokenResponseDto { IsValid = false };
        }
    }

    /// <summary>POST /api/v1/auth/reset-password</summary>
    public async Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("auth/reset-password", new
            {
                token           = request.Token,
                newPassword     = request.NewPassword,
                confirmPassword = request.ConfirmPassword
            }, ct);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Password reset failed");
            return false;
        }
    }
}
