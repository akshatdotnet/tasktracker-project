using TaskTracker.Domain.Common;
using TaskTracker.Domain.Enums;

namespace TaskTracker.Domain.Entities;

/// <summary>
/// User aggregate root — owns authentication state, credentials, and role.
/// Uses private setters to protect invariants (DDD encapsulation).
/// Business rules: locking, credential rotation, and login recording live here.
/// </summary>
public sealed class User : AuditableEntity
{
    // ── Identity ─────────────────────────────────────────────────────────
    public string   Email        { get; private set; } = default!;
    public string   PasswordHash { get; private set; } = default!;
    public string   Salt         { get; private set; } = default!;
    public string   FirstName    { get; private set; } = default!;
    public string   LastName     { get; private set; } = default!;
    public UserRole Role         { get; private set; }
    public bool     IsActive     { get; private set; }

    // ── Login tracking ───────────────────────────────────────────────────
    public DateTime? LastLoginAt    { get; private set; }
    public int       FailedLoginCount { get; private set; }
    public DateTime? LockoutEnd     { get; private set; }

    // ── Navigation properties ────────────────────────────────────────────
    public ICollection<RefreshToken>      RefreshTokens      { get; private set; } = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; private set; } = new List<PasswordResetToken>();
    public ICollection<TaskItem>          AssignedTasks      { get; private set; } = new List<TaskItem>();
    public ICollection<TimeLog>           TimeLogs           { get; private set; } = new List<TimeLog>();

    // EF Core requires parameterless constructor
    private User() { }

    /// <summary>Factory method — the only way to create a valid User.</summary>
    public static User Create(
        string email, string passwordHash, string salt,
        string firstName, string lastName,
        UserRole role = UserRole.Developer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Email        = email.ToLowerInvariant().Trim(),
            PasswordHash = passwordHash,
            Salt         = salt,
            FirstName    = firstName.Trim(),
            LastName     = lastName.Trim(),
            Role         = role,
            IsActive     = true,
        };
    }

    /// <summary>Computed full name — avoids storing derived data.</summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Rotate password and clear lockout state.</summary>
    public void UpdatePassword(string passwordHash, string salt)
    {
        PasswordHash      = passwordHash;
        Salt              = salt;
        UpdatedAt         = DateTime.UtcNow;
        FailedLoginCount  = 0;
        LockoutEnd        = null;
    }

    /// <summary>Record a successful login — resets failure counter.</summary>
    public void RecordSuccessfulLogin()
    {
        LastLoginAt      = DateTime.UtcNow;
        FailedLoginCount = 0;
        LockoutEnd       = null;
    }

    /// <summary>
    /// Record a failed login. After 5 failures the account is locked for 15 minutes.
    /// OWASP: Brute-force protection.
    /// </summary>
    public void RecordFailedLogin()
    {
        FailedLoginCount++;
        if (FailedLoginCount >= 5)
            LockoutEnd = DateTime.UtcNow.AddMinutes(15);
    }

    /// <summary>Returns true if the account is currently in lockout period.</summary>
    public bool IsLockedOut() =>
        LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate()   { IsActive = true;  UpdatedAt = DateTime.UtcNow; }
}

/// <summary>
/// Refresh token entity — one-to-many with User.
/// Rotation: every use invalidates this token and issues a new one (OWASP).
/// Only the SHA-256 hash is stored — never the raw token value.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public Guid     UserId     { get; private set; }
    public string   TokenHash  { get; private set; } = default!; // SHA-256 of raw token
    public DateTime ExpiresAt  { get; private set; }
    public bool     IsRevoked  { get; private set; }
    public DateTime CreatedAt  { get; private set; } = DateTime.UtcNow;
    public string?  IpAddress  { get; private set; }

    public User User { get; private set; } = default!;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, string? ipAddress = null)
        => new()
        {
            UserId    = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IpAddress = ipAddress,
        };

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive  => !IsRevoked && !IsExpired;

    /// <summary>Revoke this token — called on rotation or sign-out.</summary>
    public void Revoke() => IsRevoked = true;
}

/// <summary>
/// Password reset token — cryptographically secure, 15-minute expiry.
/// Hash stored, raw token emailed to user.
/// OWASP: No email enumeration — same response regardless of email existence.
/// </summary>
public sealed class PasswordResetToken : BaseEntity
{
    public Guid     UserId     { get; private set; }
    public string   TokenHash  { get; private set; } = default!;
    public DateTime ExpiresAt  { get; private set; }
    public DateTime? UsedAt    { get; private set; }
    public string?  IpAddress  { get; private set; }
    public DateTime CreatedAt  { get; private set; } = DateTime.UtcNow;

    public User User { get; private set; } = default!;

    private PasswordResetToken() { }

    public static PasswordResetToken Create(Guid userId, string tokenHash, string? ipAddress = null)
        => new()
        {
            UserId    = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IpAddress = ipAddress,
        };

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed    => UsedAt.HasValue;
    public bool IsValid   => !IsExpired && !IsUsed;

    /// <summary>Mark token as consumed — prevents replay.</summary>
    public void MarkUsed() => UsedAt = DateTime.UtcNow;
}
