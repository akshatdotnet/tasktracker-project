namespace TaskTracker.MVC.Domain.Models;

/// <summary>
/// Represents an authenticated user session stored in the cookie/session.
/// Maps to the JWT claims extracted after login.
/// </summary>
public sealed class AuthenticatedUser
{
    public string UserId   { get; init; } = string.Empty;
    public string Email    { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role     { get; init; } = string.Empty;

    public bool IsAdmin     => Role == "Admin";
    public bool IsDeveloper => Role == "Developer";
    public bool IsViewer    => Role == "Viewer";
}

/// <summary>
/// Token pair stored in the encrypted session cookie.
/// Never exposed to the browser directly (HttpOnly cookie).
/// </summary>
public sealed class TokenPair
{
    public string AccessToken  { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt  { get; init; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
