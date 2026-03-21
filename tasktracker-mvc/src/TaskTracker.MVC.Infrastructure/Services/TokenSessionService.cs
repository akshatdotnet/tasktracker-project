using Microsoft.AspNetCore.Http;
using System.Text.Json;
using TaskTracker.MVC.Application.DTOs.Auth;
using TaskTracker.MVC.Application.Interfaces;

namespace TaskTracker.MVC.Infrastructure.Services;

/// <summary>
/// Stores JWT tokens in server-side encrypted session (not in browser localStorage).
/// This is MORE secure than the Angular SPA approach — the browser never sees raw tokens.
/// The session cookie is HttpOnly + Secure + SameSite=Strict.
/// SRP: only manages token persistence in ASP.NET Core session.
/// </summary>
public sealed class TokenSessionService : ITokenSessionService
{
    private const string AccessTokenKey  = "_tt_access";
    private const string RefreshTokenKey = "_tt_refresh";
    private const string ExpiryKey       = "_tt_expiry";
    private const string UserIdKey       = "_tt_uid";
    private const string UserRoleKey     = "_tt_role";
    private const string UserNameKey     = "_tt_name";

    private readonly IHttpContextAccessor _http;

    public TokenSessionService(IHttpContextAccessor http) => _http = http;

    private ISession Session => _http.HttpContext!.Session;

    public void SaveTokens(LoginResponseDto response)
    {
        Session.SetString(AccessTokenKey,  response.AccessToken);
        Session.SetString(RefreshTokenKey, response.RefreshToken);
        Session.SetString(ExpiryKey,       response.ExpiresAt.ToString("O")); // ISO 8601
        Session.SetString(UserIdKey,       response.UserId);
        Session.SetString(UserRoleKey,     response.Role);
        Session.SetString(UserNameKey,     response.FullName);
    }

    public string? GetAccessToken()  => Session.GetString(AccessTokenKey);
    public string? GetRefreshToken() => Session.GetString(RefreshTokenKey);
    public string? GetUserId()       => Session.GetString(UserIdKey);
    public string? GetUserRole()     => Session.GetString(UserRoleKey);
    public string? GetUserFullName() => Session.GetString(UserNameKey);

    public bool IsExpired()
    {
        var expiryStr = Session.GetString(ExpiryKey);
        if (string.IsNullOrEmpty(expiryStr)) return true;
        return !DateTime.TryParse(expiryStr, out var expiry) || DateTime.UtcNow >= expiry;
    }

    public bool IsAuthenticated()
        => !string.IsNullOrEmpty(GetAccessToken()) && !IsExpired();

    public void ClearTokens()
    {
        Session.Remove(AccessTokenKey);
        Session.Remove(RefreshTokenKey);
        Session.Remove(ExpiryKey);
        Session.Remove(UserIdKey);
        Session.Remove(UserRoleKey);
        Session.Remove(UserNameKey);
    }
}
