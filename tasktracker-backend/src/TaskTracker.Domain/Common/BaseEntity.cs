namespace TaskTracker.Domain.Common;

/// <summary>
/// Base entity with a strongly-typed GUID identity.
/// All domain entities derive from this to ensure consistent identity management.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Unique identifier generated at construction time.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

/// <summary>
/// Auditable entity — automatically tracks who created/modified the record and when.
/// Infrastructure layer (EF Core SaveChanges override) populates UpdatedAt automatically.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string?  CreatedBy  { get; set; }
    public string?  UpdatedBy  { get; set; }
}

/// <summary>
/// Discriminated Result type — implements the Result Pattern.
/// Handlers return Result/Result&lt;T&gt; instead of throwing exceptions for control flow.
/// Only infrastructure failures (DB down, network unreachable) should throw exceptions.
///
/// SOLID: Open/Closed — new result types extend without modifying base.
/// </summary>
public class Result
{
    public bool    IsSuccess { get; protected set; }
    public string? Error     { get; protected set; }
    public bool    IsFailure => !IsSuccess;

    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error     = error;
    }

    public static Result          Success()                => new(true,  null);
    public static Result          Failure(string error)    => new(false, error);
    public static Result<T>       Success<T>(T value)      => new(value, true,  null);
    public static Result<T>       Failure<T>(string error) => new(default!, false, error);
}

/// <summary>Generic Result carrying a value on success.</summary>
public sealed class Result<T> : Result
{
    /// <summary>The value — only valid when IsSuccess is true.</summary>
    public T Value { get; }

    internal Result(T value, bool isSuccess, string? error)
        : base(isSuccess, error) => Value = value;
}
