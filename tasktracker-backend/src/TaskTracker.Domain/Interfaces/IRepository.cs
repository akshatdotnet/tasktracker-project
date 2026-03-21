using TaskTracker.Domain.Common;
using TaskTracker.Domain.Entities;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Domain.Interfaces;

// ── Unit of Work ──────────────────────────────────────────────────────────────

/// <summary>
/// Unit of Work — coordinates multiple repositories in a single transaction.
/// Handlers call SaveChangesAsync once after all operations are complete.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// ── Generic Repository ────────────────────────────────────────────────────────

/// <summary>
/// Generic repository contract (ISP: clients depend only on what they need).
/// Concrete implementations live in Infrastructure — Domain has no EF Core dependency.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?>              GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task                  AddAsync(T entity, CancellationToken ct = default);
    void                  Update(T entity);
    void                  Remove(T entity);
}

// ── Specific Repository Contracts ─────────────────────────────────────────────

public interface IUserRepository : IRepository<User>
{
    /// <summary>Case-insensitive email lookup.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Includes refresh and reset tokens for token operations.</summary>
    Task<User?> GetByIdWithTokensAsync(Guid id, CancellationToken ct = default);

    Task<bool>  ExistsAsync(string email, CancellationToken ct = default);
}

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task                RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task                      RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IProjectRepository : IRepository<Project>
{
    Task<Project?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetActiveProjectsAsync(CancellationToken ct = default);
}

public interface ISprintRepository : IRepository<Sprint>
{
    Task<Sprint?> GetActiveSprintAsync(Guid projectId, CancellationToken ct = default);
}

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<IReadOnlyList<TaskItem>> GetByAssigneeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tasks touched (started, completed, or updated) today.</summary>
    Task<IReadOnlyList<TaskItem>> GetByAssigneeTodayAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<TaskItem>>       GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<Dictionary<TaskStatus, int>>   GetStatusBreakdownAsync(CancellationToken ct = default);
    Task<int>                           CountCompletedTodayAsync(CancellationToken ct = default);
}

public interface ITimeLogRepository : IRepository<TimeLog>
{
    Task<IReadOnlyList<TimeLog>> GetByUserTodayAsync(Guid userId, CancellationToken ct = default);
    Task<double>                 GetTotalHoursByUserTodayAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns per-day task count and hours for the last N days.</summary>
    Task<IReadOnlyList<(DateTime Date, int TasksCompleted, double HoursLogged)>>
        GetVelocityAsync(int days, CancellationToken ct = default);
}
