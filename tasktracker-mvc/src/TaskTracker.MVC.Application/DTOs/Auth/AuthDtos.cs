using System.ComponentModel.DataAnnotations;

namespace TaskTracker.MVC.Application.DTOs.Auth;

// ── Request DTOs (bound from HTML form POST) ──────────────────────────────────

public sealed class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [Display(Name = "Password")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequestDto
{
    [Required] public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [Display(Name = "New Password")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ── Response DTOs (deserialized from API JSON) ────────────────────────────────

public sealed class LoginResponseDto
{
    public string   AccessToken  { get; set; } = string.Empty;
    public string   RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt    { get; set; }
    public string   UserId       { get; set; } = string.Empty;
    public string   Role         { get; set; } = string.Empty;
    public string   FullName     { get; set; } = string.Empty;
}

public sealed class ValidateTokenResponseDto
{
    public bool      IsValid   { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
