using Microsoft.EntityFrameworkCore;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Interfaces;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Infrastructure.Persistence.Repositories;

// ── User Repository ───────────────────────────────────────────────────────────
public sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _dbSet
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant().Trim(), ct);

    public async Task<User?> GetByIdWithTokensAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Include(u => u.RefreshTokens)
            .Include(u => u.PasswordResetTokens)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
        => await _dbSet.AnyAsync(u => u.Email == email.ToLowerInvariant().Trim(), ct);

    public override async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.Where(u => u.IsActive).OrderBy(u => u.FirstName).ToListAsync(ct);
}

// ── RefreshToken Repository ───────────────────────────────────────────────────
public sealed class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context) : base(context) { }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => await _dbSet
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _dbSet
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(ct);

        foreach (var t in tokens) t.Revoke();
    }
}

// ── PasswordResetToken Repository ────────────────────────────────────────────
public sealed class PasswordResetTokenRepository : Repository<PasswordResetToken>, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(AppDbContext context) : base(context) { }

    public async Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => await _dbSet
            .Include(pr => pr.User)
            .FirstOrDefaultAsync(pr => pr.TokenHash == tokenHash, ct);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Mark all existing unused tokens as used (invalidated)
        var tokens = await _dbSet
            .Where(pr => pr.UserId == userId && !pr.IsUsed && !pr.IsExpired)
            .ToListAsync(ct);

        foreach (var t in tokens) t.MarkUsed();
    }
}

// ── Project Repository ────────────────────────────────────────────────────────
public sealed class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(AppDbContext context) : base(context) { }

    public async Task<Project?> GetByNameAsync(string name, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(p => p.Name == name, ct);

    public async Task<IReadOnlyList<Project>> GetActiveProjectsAsync(CancellationToken ct = default)
        => await _dbSet.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);
}

// ── Sprint Repository ─────────────────────────────────────────────────────────
public sealed class SprintRepository : Repository<Sprint>, ISprintRepository
{
    public SprintRepository(AppDbContext context) : base(context) { }

    public async Task<Sprint?> GetActiveSprintAsync(Guid projectId, CancellationToken ct = default)
        => await _dbSet
            .Where(s => s.ProjectId == projectId && s.IsActive)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);
}

// ── Task Repository ───────────────────────────────────────────────────────────
public sealed class TaskRepository : Repository<TaskItem>, ITaskRepository
{
    public TaskRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<TaskItem>> GetByAssigneeAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet
            .Include(t => t.TimeLogs)
            .Where(t => t.AssignedToId == userId)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByAssigneeTodayAsync(Guid userId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return await _dbSet
            .Include(t => t.TimeLogs)
            .Where(t => t.AssignedToId == userId &&
                (
                    // Started today
                    (t.StartedAt.HasValue && t.StartedAt.Value >= today && t.StartedAt.Value < tomorrow) ||
                    // Completed today
                    (t.CompletedAt.HasValue && t.CompletedAt.Value >= today && t.CompletedAt.Value < tomorrow) ||
                    // Updated today
                    (t.UpdatedAt.HasValue && t.UpdatedAt.Value >= today && t.UpdatedAt.Value < tomorrow) ||
                    // Currently in progress (always show)
                    t.Status == TaskStatus.InProgress
                ))
            .OrderBy(t => t.StartedAt ?? t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
        => await _dbSet
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<Dictionary<TaskStatus, int>> GetStatusBreakdownAsync(CancellationToken ct = default)
    {
        var counts = await _dbSet
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => x.Status, x => x.Count);
    }

    public async Task<int> CountCompletedTodayAsync(CancellationToken ct = default)
    {
        var today    = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return await _dbSet.CountAsync(
            t => t.Status == TaskStatus.Completed &&
                 t.CompletedAt.HasValue &&
                 t.CompletedAt.Value >= today &&
                 t.CompletedAt.Value < tomorrow, ct);
    }
}

// ── TimeLog Repository ────────────────────────────────────────────────────────
public sealed class TimeLogRepository : Repository<TimeLog>, ITimeLogRepository
{
    public TimeLogRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<TimeLog>> GetByUserTodayAsync(Guid userId, CancellationToken ct = default)
    {
        var today    = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return await _dbSet
            .Include(tl => tl.Task)
            .Where(tl => tl.UserId == userId &&
                         tl.LoggedAt >= today &&
                         tl.LoggedAt < tomorrow)
            .OrderBy(tl => tl.LoggedAt)
            .ToListAsync(ct);
    }

    public async Task<double> GetTotalHoursByUserTodayAsync(Guid userId, CancellationToken ct = default)
    {
        var today    = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var totalMinutes = await _dbSet
            .Where(tl => tl.UserId == userId &&
                         tl.LoggedAt >= today &&
                         tl.LoggedAt < tomorrow)
            .SumAsync(tl => tl.DurationMinutes, ct);

        return Math.Round(totalMinutes / 60.0, 2);
    }

    public async Task<IReadOnlyList<(DateTime Date, int TasksCompleted, double HoursLogged)>>
        GetVelocityAsync(int days, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days + 1);

        var logs = await _dbSet
            .Where(tl => tl.LoggedAt >= from)
            .GroupBy(tl => tl.LoggedAt.Date)
            .Select(g => new
            {
                Date         = g.Key,
                HoursLogged  = g.Sum(tl => tl.DurationMinutes) / 60.0,
                TasksLogged  = g.Select(tl => tl.TaskId).Distinct().Count()
            })
            .OrderBy(g => g.Date)
            .ToListAsync(ct);

        // Fill in days with no logs as zeros
        var result = new List<(DateTime, int, double)>();
        for (int i = 0; i < days; i++)
        {
            var date    = from.AddDays(i);
            var dayData = logs.FirstOrDefault(l => l.Date == date);
            result.Add((date, dayData?.TasksLogged ?? 0, Math.Round(dayData?.HoursLogged ?? 0, 2)));
        }

        return result;
    }
}
